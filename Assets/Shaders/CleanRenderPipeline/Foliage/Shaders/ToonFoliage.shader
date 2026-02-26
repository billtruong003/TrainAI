Shader "CleanRender/ToonFoliage"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white"{}
        [MainColor] _BaseColor("Base Color", Color) = (0.3, 0.65, 0.2, 1)

        [KeywordEnum(LEAF, GRASS)] _FOLIAGE_TYPE("Foliage Type", Float) = 0

        [Header(Wind)]
        _WindTex("Wind Noise Texture", 2D) = "gray"{}
        _WindScale("Wind Scale", Float) = 0.05
        _WindSpeed("Wind Speed", Float) = 1.0
        _WindStrength("Wind Strength", Range(0, 2)) = 0.5
        [Toggle(_USE_VERTEX_COLOR_WIND)] _UseVertexColorWind("Vertex Color masks wind (R)", Float) = 1

        [Header(Cel Shading)]
        _ShadowColor("Shadow Color", Color) = (0.15, 0.25, 0.1, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.45
        _Smoothness("Shadow Smoothness", Range(0.001, 0.5)) = 0.08

        [Header(Subsurface)]
        _SubsurfaceColor("Subsurface Color", Color) = (0.5, 0.8, 0.1, 1)
        _SubsurfaceStrength("Subsurface Strength", Range(0, 1)) = 0.3

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
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/NoiseLib.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            float4 _WindTex_ST;
            half   _WindScale;
            half   _WindSpeed;
            half   _WindStrength;
            half4  _ShadowColor;
            half   _Threshold;
            half   _Smoothness;
            half4  _SubsurfaceColor;
            half   _SubsurfaceStrength;
            half   _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
        TEXTURE2D(_WindTex);  SAMPLER(sampler_WindTex);
        ENDHLSL

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PASS 0: Forward Lit
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            AlphaToMask [_ALPHATEST_ON]

            HLSLPROGRAM
            #pragma vertex FoliageVert
            #pragma fragment FoliageFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_instancing

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _USE_VERTEX_COLOR_WIND
            #pragma multi_compile_local _FOLIAGE_TYPE_LEAF _FOLIAGE_TYPE_GRASS

            struct Attributes
            {
                float4 positionOS  : POSITION;
                half3  normalOS    : NORMAL;
                float2 uv          : TEXCOORD0;
                float2 lightmapUV  : TEXCOORD1;
                half4  color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half   vertGreen  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings FoliageVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // ── Wind mask ──
                #ifdef _USE_VERTEX_COLOR_WIND
                    half windMask = input.color.r;
                #else
                    half windMask = saturate(input.positionOS.y);
                #endif

                // ── Wind displacement (texture-based) ──
                float3 windOffset = SampleWind(
                    TEXTURE2D_ARGS(_WindTex, sampler_WindTex),
                    posWS, _WindScale, _WindSpeed, _WindStrength, _Time.y);
                posWS += windOffset * windMask;

                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                o.uv.xy      = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;

                #ifdef LIGHTMAP_ON
                    o.uv.zw = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                #else
                    o.uv.zw = 0;
                #endif

                o.vertGreen = input.color.g;
                return o;
            }

            half4 FoliageFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy) * _BaseColor;

                #ifdef _ALPHATEST_ON
                    clip(albedo.a - _Cutoff);
                #endif

                half3 N = normalize(input.normalWS);
                half3 V = (half3)normalize(GetCameraPositionWS() - input.positionWS);

                // ── Main light + cel shading ──
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half NdotL = saturate(dot(N, (half3)mainLight.direction));
                half shadow = (half)mainLight.shadowAttenuation;
                half intensity = smoothstep(_Threshold - _Smoothness, _Threshold + _Smoothness, NdotL * shadow);

                half3 litColor = albedo.rgb * lerp(_ShadowColor.rgb, (half3)mainLight.color, intensity);

                // ── Subsurface scattering (leaf only) ──
                #if defined(_FOLIAGE_TYPE_LEAF)
                    half subsurface = saturate(dot(-N, (half3)mainLight.direction)) * _SubsurfaceStrength * shadow;
                    litColor += _SubsurfaceColor.rgb * subsurface * albedo.rgb;
                #endif

                // ── Vertex color AO ──
                litColor *= lerp(0.6h, 1.0h, input.vertGreen);

                // ── GI ──
                #ifdef LIGHTMAP_ON
                    litColor += SampleLightmap(input.uv.zw, N) * albedo.rgb;
                #else
                    litColor += SampleSH(N) * albedo.rgb * 0.3h;
                #endif

                return half4(litColor, albedo.a);
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
            Cull Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex FoliageShadowVert
            #pragma fragment FoliageShadowFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _USE_VERTEX_COLOR_WIND

            float3 _LightDirection;

            struct ShadowAttr
            {
                float4 positionOS : POSITION;
                half3  normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVary
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVary FoliageShadowVert(ShadowAttr input)
            {
                ShadowVary o = (ShadowVary)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // ── Match wind displacement for shadow alignment ──
                #ifdef _USE_VERTEX_COLOR_WIND
                    half windMask = input.color.r;
                #else
                    half windMask = saturate(input.positionOS.y);
                #endif

                float3 windOffset = SampleWind(
                    TEXTURE2D_ARGS(_WindTex, sampler_WindTex),
                    posWS, _WindScale, _WindSpeed, _WindStrength, _Time.y);
                posWS += windOffset * windMask;

                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                #ifdef _ALPHATEST_ON
                    o.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                #else
                    o.uv = 0;
                #endif

                return o;
            }

            half4 FoliageShadowFrag(ShadowVary input) : SV_Target
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex FoliageDepthVert
            #pragma fragment FoliageDepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _USE_VERTEX_COLOR_WIND

            struct DepthAttr { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DepthVary { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            DepthVary FoliageDepthVert(DepthAttr input)
            {
                DepthVary o;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                #ifdef _USE_VERTEX_COLOR_WIND
                    half windMask = input.color.r;
                #else
                    half windMask = saturate(input.positionOS.y);
                #endif

                float3 windOffset = SampleWind(
                    TEXTURE2D_ARGS(_WindTex, sampler_WindTex),
                    posWS, _WindScale, _WindSpeed, _WindStrength, _Time.y);
                posWS += windOffset * windMask;

                o.positionCS = TransformWorldToHClip(posWS);

                #ifdef _ALPHATEST_ON
                    o.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                #else
                    o.uv = 0;
                #endif
                return o;
            }

            half4 FoliageDepthFrag(DepthVary input) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }
}
