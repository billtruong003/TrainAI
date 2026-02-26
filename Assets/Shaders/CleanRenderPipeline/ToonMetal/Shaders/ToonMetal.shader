Shader "CleanRender/ToonMetal"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white"{}
        [MainColor] _BaseColor("Base Color", Color) = (0.8, 0.75, 0.65, 1)

        [Header(Metal)]
        _MetalColor("Metal Highlight Color", Color) = (1, 0.95, 0.85, 1)
        _MetalCutoff("Metal Fresnel Cutoff", Range(0, 1)) = 0.5
        _MetalSmoothness("Metal Fresnel Smoothness", Range(0, 0.3)) = 0.05
        _MetalMask("Metal Mask (R=metal)", 2D) = "white"{}

        [Header(Specular Highlight)]
        _SpecColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecCutoff("Specular Cutoff", Range(0, 1)) = 0.85
        _SpecSmoothness("Specular Smoothness", Range(0, 0.2)) = 0.03

        [Header(Cel Shading)]
        _ShadowColor("Shadow Color", Color) = (0.25, 0.2, 0.35, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.45
        _Smoothness("Shadow Smoothness", Range(0, 0.5)) = 0.04

        [Header(Rim)]
        _RimColor("Rim Color", Color) = (1, 0.95, 0.9, 0.6)
        _RimPower("Rim Power", Range(0.1, 10)) = 4

        [Header(Alpha)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/ToonLighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _MetalColor;
            half   _MetalCutoff;
            half   _MetalSmoothness;
            half4  _SpecColor;
            half   _SpecCutoff;
            half   _SpecSmoothness;
            half4  _ShadowColor;
            half4  _RimColor;
            half   _Threshold;
            half   _Smoothness;
            half   _RimPower;
            half   _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MetalMask);  SAMPLER(sampler_MetalMask);
        ENDHLSL

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PASS 0: Forward Lit
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex MetalVert
            #pragma fragment MetalFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3  normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings MetalVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                o.uv         = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return o;
            }

            half4 MetalFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedoTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half metalMask  = SAMPLE_TEXTURE2D(_MetalMask, sampler_MetalMask, input.uv).r;

                #ifdef _ALPHATEST_ON
                    clip(albedoTex.a * _BaseColor.a - _Cutoff);
                #endif

                half3 N = normalize(input.normalWS);
                half3 V = (half3)normalize(GetCameraPositionWS() - input.positionWS);

                // ── Main Light ──
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 L = (half3)mainLight.direction;
                half3 lightColor = (half3)mainLight.color;

                half NdotL = dot(N, L);
                half shadow = (half)mainLight.shadowAttenuation;
                half intensity = smoothstep(_Threshold - _Smoothness, _Threshold + _Smoothness, NdotL * shadow);

                // ── Base Cel Shading ──
                half3 baseAlbedo = albedoTex.rgb * _BaseColor.rgb;
                half3 diffuse = baseAlbedo * lerp(_ShadowColor.rgb, lightColor, intensity);

                // ── Metallic Fresnel ──
                half NdotV = dot(N, V);
                half fresnel = smoothstep(_MetalCutoff - _MetalSmoothness,
                                          _MetalCutoff + _MetalSmoothness, NdotV);
                half3 metalHighlight = lerp(baseAlbedo * _ShadowColor.rgb, _MetalColor.rgb, fresnel);

                // ── Specular (half-vector based) ──
                half3 H = normalize(L + V);
                half NdotH = saturate(dot(N, H));
                half spec = smoothstep(_SpecCutoff - _SpecSmoothness,
                                       _SpecCutoff + _SpecSmoothness, NdotH) * shadow;
                half3 specular = _SpecColor.rgb * spec;

                // ── Combine: blend standard toon vs metal ──
                half3 metal = metalHighlight * lerp(_ShadowColor.rgb, lightColor, intensity) + specular;
                half3 finalColor = lerp(diffuse, metal, metalMask);

                // ── Rim ──
                half3 rim = ComputeRim(N, V, intensity, _RimColor.rgb, _RimPower, _RimColor.a);
                finalColor += rim;

                // ── Ambient ──
                finalColor += SampleSH(N) * baseAlbedo * 0.3h;

                // ── Additional Lights ──
                #ifdef _ADDITIONAL_LIGHTS
                    finalColor += ComputeToonAdditionalLightsGlobal(input.positionWS, N, baseAlbedo);
                #endif

                return half4(finalColor, albedoTex.a * _BaseColor.a);
            }
            ENDHLSL
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PASS 1: Shadow Caster
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            float3 _LightDirection;

            ShadowCasterVaryings ShadowVert(ShadowCasterAttributes input)
            {
                ShadowCasterVaryings o = (ShadowCasterVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformShadowCasterPositionCS(
                    input.positionOS.xyz, input.normalOS, _LightDirection);

                #ifdef _ALPHATEST_ON
                    o.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                #else
                    o.uv = 0;
                #endif
                return o;
            }

            half4 ShadowFrag(ShadowCasterVaryings input) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PASS 2: Depth Only
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DOVert
            #pragma fragment DOFrag
            #pragma multi_compile_instancing

            struct DOAttr { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DOVary { float4 positionCS : SV_POSITION; };

            DOVary DOVert(DOAttr input)
            {
                DOVary o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }
            half4 DOFrag(DOVary i) : SV_Target { return 0; }
            ENDHLSL
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PASS 3: Depth Normals
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            ColorMask RGBA

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing

            struct DNAttr { float4 positionOS : POSITION; half3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DNVary { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            DNVary DNVert(DNAttr input)
            {
                DNVary o = (DNVary)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DNFrag(DNVary input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                return half4(n * 0.5h + 0.5h, 0);
            }
            ENDHLSL
        }
    }
}
