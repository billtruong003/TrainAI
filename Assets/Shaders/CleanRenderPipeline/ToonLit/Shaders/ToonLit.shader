Shader "CleanRender/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white"{}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Toggle(_USE_LOCAL_TOON)] _UseLocalToon("Use Local Params", Float) = 0
        _ShadowColor("Shadow Color", Color) = (0.3, 0.3, 0.4, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.5
        _Smoothness("Shadow Smoothness", Range(0.001, 0.5)) = 0.05

        _RimColor("Rim Color", Color) = (1, 1, 1, 0.5)
        _RimPower("Rim Power", Range(0.1, 10)) = 3

        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [Toggle(_EMISSION)] _Emission("Emission", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0)
        _EmissionMap("Emission Map", 2D) = "white"{}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
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
            half4  _ShadowColor;
            half4  _RimColor;
            half   _Threshold;
            half   _Smoothness;
            half   _RimPower;
            half   _Cutoff;
            half4  _EmissionColor;
        CBUFFER_END

        TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);    SAMPLER(sampler_EmissionMap);
        ENDHLSL

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PASS 0: Forward Lit
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            AlphaToMask [_ALPHATEST_ON]

            HLSLPROGRAM
            #pragma vertex ToonLitVert
            #pragma fragment ToonLitFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_instancing

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _USE_LOCAL_TOON

            struct Attributes
            {
                float4 positionOS  : POSITION;
                half3  normalOS    : NORMAL;
                float2 uv          : TEXCOORD0;
                float2 lightmapUV  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv         : TEXCOORD0;   // xy = baseUV, zw = lightmapUV
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonLitVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Single transform chain — minimal register usage
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                o.uv.xy      = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;

                #ifdef LIGHTMAP_ON
                    o.uv.zw = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                #else
                    o.uv.zw = 0;
                #endif

                return o;
            }

            half4 ToonLitFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy) * _BaseColor;

                #ifdef _ALPHATEST_ON
                    clip(albedo.a - _Cutoff);
                #endif

                half3 normalWS  = normalize(input.normalWS);
                half3 viewDirWS = (half3)normalize(GetCameraPositionWS() - input.positionWS);

                #ifdef _USE_LOCAL_TOON
                    ToonLightResult lit = ComputeToonMainLight(
                        input.positionWS, normalWS, viewDirWS, albedo.rgb, input.uv.zw,
                        _Threshold, _Smoothness, _ShadowColor.rgb,
                        _RimColor.rgb, _RimPower, _RimColor.a, 1.0h);
                #else
                    ToonLightResult lit = ComputeToonMainLightGlobal(
                        input.positionWS, normalWS, viewDirWS, albedo.rgb, input.uv.zw);
                #endif

                half3 finalColor = lit.diffuse + lit.rim + lit.globalIllumination;

                #ifdef _ADDITIONAL_LIGHTS
                    finalColor += ComputeToonAdditionalLightsGlobal(input.positionWS, normalWS, albedo.rgb);
                #endif

                #ifdef _EMISSION
                    finalColor += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv.xy).rgb * _EmissionColor.rgb;
                #endif

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PASS 1: Shadow Caster
        // CRITICAL: This pass enables objects to cast shadows onto terrain/others
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
            float3 _LightPosition; // URP 12+ uses this for point light shadows

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
                UNITY_SETUP_INSTANCE_ID(input);
                #ifdef _ALPHATEST_ON
                    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                    clip(alpha - _Cutoff);
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
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            struct DepthAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DepthVaryings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings o = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                #ifdef _ALPHATEST_ON
                    o.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                #else
                    o.uv = 0;
                #endif
                return o;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PASS 3: Depth Normals (for SSAO etc)
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            ColorMask RGBA

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            struct DNAttributes { float4 positionOS : POSITION; half3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DNVaryings   { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            DNVaryings DepthNormalsVert(DNAttributes input)
            {
                DNVaryings o = (DNVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DepthNormalsFrag(DNVaryings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                return half4(n * 0.5h + 0.5h, 0);
            }
            ENDHLSL
        }
    }
}
