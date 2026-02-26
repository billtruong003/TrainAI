Shader "VR/StylizedWater"
{
    Properties
    {
        [HideInInspector] _Cull("Cull", Float) = 2.0

        [Header(Color)]
        _ShallowColor("Shallow Color", Color) = (0.3, 0.8, 0.9, 0.7)
        [HDR] _DeepColor("Deep Color", Color) = (0.0, 0.2, 0.4, 0.8)
        _DepthMaxDistance("Depth Max Distance", Range(0.1, 20.0)) = 5.0

        [Header(Normal)]
        _NormalMap("Water Normal Map", 2D) = "bump" {}
        _NormalTilingA("Normal Tiling A", Float) = 1.0
        _NormalScrollA("Normal Scroll A", Vector) = (0.01, 0.02, 0, 0)
        _NormalTilingB("Normal Tiling B", Float) = 1.5
        _NormalScrollB("Normal Scroll B", Vector) = (-0.015, 0.01, 0, 0)
        _NormalStrength("Normal Strength", Range(0.0, 2.0)) = 1.0

        [Header(Refraction)]
        [Toggle(_REFRACTION)] _UseRefraction("Enable Refraction", Float) = 1
        _RefractionStrength("Refraction Strength", Range(0.0, 0.2)) = 0.05

        [Header(Surface Foam)]
        _SurfaceFoamTexture("Surface Foam Texture", 2D) = "white" {}
        _SurfaceFoamTiling("Surface Foam Tiling", Float) = 2.0
        _SurfaceFoamScroll("Surface Foam Scroll", Vector) = (0.02, 0.025, 0, 0)
        _SurfaceFoamCutoff("Surface Foam Cutoff", Range(0.0, 1.0)) = 0.6
        _FoamDistortion("Foam Distortion", Range(0.0, 0.5)) = 0.1

        [Header(Intersection Foam)]
        _FoamColor("Intersection Foam Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _FoamIntersectionDepth("Intersection Depth", Range(0.01, 5.0)) = 0.8
        _FoamEdgeSmoothness("Intersection Edge Smoothness", Range(0, 1)) = 0.5

        [Header(Specular Bling)]
        [HDR] _BlingColor("Bling Color", Color) = (2.0, 2.0, 2.0, 1.0)
        _BlingGloss("Bling Gloss", Range(10.0, 1024.0)) = 256.0
        _BlingThreshold("Bling Threshold", Range(0.0, 1.0)) = 0.8
        _BlingIntensity("Bling Intensity", Range(0.0, 10.0)) = 2.0

        [Header(Waves)]
        _WaveAmplitude("Wave Amplitude", Range(0.0, 2.0)) = 0.15
        _WaveFrequency("Wave Frequency", Float) = 1.5
        _WaveSpeed("Wave Speed", Float) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4  _ShallowColor;
            half4  _DeepColor;
            half4  _FoamColor;
            half4  _BlingColor;
            float4 _NormalScrollA;
            float4 _NormalScrollB;
            float4 _SurfaceFoamScroll;
            half   _DepthMaxDistance;
            half   _NormalTilingA;
            half   _NormalTilingB;
            half   _NormalStrength;
            half   _RefractionStrength;
            half   _SurfaceFoamTiling;
            half   _SurfaceFoamCutoff;
            half   _FoamDistortion;
            half   _FoamIntersectionDepth;
            half   _FoamEdgeSmoothness;
            half   _BlingGloss;
            half   _BlingThreshold;
            half   _BlingIntensity;
            half   _WaveAmplitude;
            half   _WaveFrequency;
            half   _WaveSpeed;
        CBUFFER_END

        TEXTURE2D(_NormalMap);           SAMPLER(sampler_NormalMap);
        TEXTURE2D(_SurfaceFoamTexture);  SAMPLER(sampler_SurfaceFoamTexture);

        // ── Optimized vertex wave: single sin, minimal ALU ──
        float GetVertexWave(float3 positionOS)
        {
            float waveInput = (positionOS.x + positionOS.z) * _WaveFrequency + _Time.y * _WaveSpeed;
            return sin(waveInput) * _WaveAmplitude;
        }
        ENDHLSL

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PASS 0: Forward Lit
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            // ★ KEY OPTIMIZATION: Refraction as shader_feature
            // When disabled, skips SampleSceneColor entirely (saves 1 fullscreen texture read)
            #pragma shader_feature_local _REFRACTION

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posOS = input.positionOS.xyz;
                posOS.y += GetVertexWave(posOS);

                output.positionWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screenPos  = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // ── Depth ──
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceDepth = input.positionCS.w;
                float depthDiff = max(0.001, sceneDepth - surfaceDepth);

                // ── Normals (2 scrolling layers) ──
                float timeY = _Time.y;
                float2 worldXZ = input.positionWS.xz;

                half3 nA = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                    worldXZ * _NormalTilingA + timeY * _NormalScrollA.xy));
                half3 nB = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                    worldXZ * _NormalTilingB + timeY * _NormalScrollB.xy));

                half3 normalWS = normalize(half3(
                    (nA.xy + nB.xy) * _NormalStrength,
                    nA.z * nB.z));

                // ── Water color (depth gradient) ──
                half depthGradient = saturate(depthDiff / _DepthMaxDistance);
                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthGradient);

                // ── Refraction (completely skipped when _REFRACTION is off) ──
                #ifdef _REFRACTION
                    float2 refractOffset = normalWS.xy * _RefractionStrength * saturate(depthDiff * 0.5);
                    half3 sceneColor = SampleSceneColor(screenUV + refractOffset);
                    half3 finalColor = lerp(waterColor, sceneColor, 1.0h - _ShallowColor.a);
                #else
                    half3 finalColor = waterColor;
                #endif

                // ── Surface foam ──
                float2 foamUV = worldXZ * _SurfaceFoamTiling + timeY * _SurfaceFoamScroll.xy;
                foamUV += normalWS.xy * _FoamDistortion;

                half foamNoise = SAMPLE_TEXTURE2D(_SurfaceFoamTexture, sampler_SurfaceFoamTexture, foamUV).r;
                half surfaceFoam = step(_SurfaceFoamCutoff, foamNoise);

                // ── Intersection foam (smoothness-controlled) ──
                half softRange = lerp(0.01h, _FoamIntersectionDepth, _FoamEdgeSmoothness);
                half intersectFoam = 1.0h - smoothstep(0.0h, softRange, (half)depthDiff);

                half combinedFoam = saturate(surfaceFoam * saturate(depthDiff / _FoamIntersectionDepth) + intersectFoam);
                finalColor = lerp(finalColor, _FoamColor.rgb, combinedFoam);

                // ── Specular bling ──
                Light mainLight = GetMainLight();
                half3 viewDir = (half3)normalize(GetCameraPositionWS() - input.positionWS);
                half3 halfVec = normalize((half3)mainLight.direction + viewDir);

                half NdotH = saturate(dot(normalWS, halfVec));
                half specular = pow(NdotH, _BlingGloss);
                half sparkleMask = step(_BlingThreshold, specular) * specular;

                finalColor += sparkleMask * _BlingColor.rgb * _BlingIntensity * (half)mainLight.shadowAttenuation;

                return half4(finalColor, _ShallowColor.a);
            }
            ENDHLSL
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PASS 1: Depth Only
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings depthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posOS = input.positionOS.xyz;
                posOS.y += GetVertexWave(posOS);
                o.positionCS = TransformObjectToHClip(posOS);
                return o;
            }

            half4 depthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }
    }
    CustomEditor "StylizedWaterVRGUI"
}
