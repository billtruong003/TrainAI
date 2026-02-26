Shader "CleanRender/ToonBark"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white"{}
        [MainColor] _BaseColor("Base Color", Color) = (0.45, 0.35, 0.25, 1)

        [Header(Wind Sway)]
        _SwaySpeed("Sway Speed", Range(0, 3)) = 0.6
        _SwayStrength("Sway Strength", Range(0, 0.3)) = 0.05
        _SwayFrequency("Sway Frequency", Range(0, 2)) = 0.3
        [Toggle(_USE_VERTEX_COLOR_WIND)] _UseVertexColorWind("Vertex Color masks wind (R)", Float) = 1

        [Header(Cel Shading)]
        [Toggle(_USE_LOCAL_TOON)] _UseLocalToon("Use Local Params", Float) = 0
        _ShadowColor("Shadow Color", Color) = (0.2, 0.15, 0.1, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.45
        _Smoothness("Shadow Smoothness", Range(0.001, 0.5)) = 0.06

        [Header(Detail)]
        _RimColor("Rim Color", Color) = (0.6, 0.5, 0.4, 0.3)
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
        LOD 150

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/ToonLighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _SwaySpeed;
            half   _SwayStrength;
            half   _SwayFrequency;
            half4  _ShadowColor;
            half   _Threshold;
            half   _Smoothness;
            half4  _RimColor;
            half   _RimPower;
            half   _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        // ── Cheap sine-based sway — NO texture read in vertex ──
        // Uses 2 sin waves for organic feel, ~4 ALU ops total
        half ComputeBarkSway(float3 posWS, half speed, half freq, float time)
        {
            half phase1 = sin(posWS.x * freq + time * speed);
            half phase2 = sin(posWS.z * freq * 0.7h + time * speed * 0.8h);
            return (phase1 + phase2) * 0.5h;
        }
        ENDHLSL

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // PASS 0: Forward Lit
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex BarkVert
            #pragma fragment BarkFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_instancing

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _USE_LOCAL_TOON
            #pragma shader_feature_local _USE_VERTEX_COLOR_WIND

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
                half   vertGreen  : TEXCOORD3; // vertex color G for AO
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings BarkVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // ── Wind mask ──
                #ifdef _USE_VERTEX_COLOR_WIND
                    half windMask = input.color.r;
                #else
                    half windMask = saturate(input.positionOS.y);
                #endif

                // ── Gentle sway (pure math, no texture read) ──
                half sway = ComputeBarkSway(posWS, _SwaySpeed, _SwayFrequency, _Time.y);
                posWS.x += sway * _SwayStrength * windMask;

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

            half4 BarkFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy) * _BaseColor;

                #ifdef _ALPHATEST_ON
                    clip(albedo.a - _Cutoff);
                #endif

                half3 N = normalize(input.normalWS);
                half3 V = (half3)normalize(GetCameraPositionWS() - input.positionWS);

                // ── Main light ──
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half NdotL = saturate(dot(N, (half3)mainLight.direction));
                half shadow = (half)mainLight.shadowAttenuation;

                #ifdef _USE_LOCAL_TOON
                    half intensity = smoothstep(_Threshold - _Smoothness, _Threshold + _Smoothness, NdotL * shadow);
                    half3 litColor = albedo.rgb * lerp(_ShadowColor.rgb, (half3)mainLight.color, intensity);
                #else
                    half intensity = smoothstep(
                        _GlobalShadowThreshold - _GlobalShadowSmoothness,
                        _GlobalShadowThreshold + _GlobalShadowSmoothness,
                        NdotL * shadow);
                    half3 litColor = albedo.rgb * lerp(_GlobalShadowColor.rgb, (half3)mainLight.color, intensity);
                #endif

                // ── Vertex color AO ──
                litColor *= lerp(0.6h, 1.0h, input.vertGreen);

                // ── Rim ──
                half3 rim = ComputeRim(N, V, intensity, _RimColor.rgb, _RimPower, _RimColor.a);
                litColor += rim;

                // ── GI ──
                #ifdef LIGHTMAP_ON
                    litColor += SampleLightmap(input.uv.zw, N) * albedo.rgb;
                #else
                    litColor += SampleSH(N) * albedo.rgb * 0.3h;
                #endif

                // ── Additional lights ──
                #ifdef _ADDITIONAL_LIGHTS
                    litColor += ComputeToonAdditionalLightsGlobal(input.positionWS, N, albedo.rgb);
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
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex BarkShadowVert
            #pragma fragment BarkShadowFrag
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

            ShadowVary BarkShadowVert(ShadowAttr input)
            {
                ShadowVary o = (ShadowVary)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // Apply same sway so shadow matches geometry
                #ifdef _USE_VERTEX_COLOR_WIND
                    half windMask = input.color.r;
                #else
                    half windMask = saturate(input.positionOS.y);
                #endif

                half sway = ComputeBarkSway(posWS, _SwaySpeed, _SwayFrequency, _Time.y);
                posWS.x += sway * _SwayStrength * windMask;

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

            half4 BarkShadowFrag(ShadowVary input) : SV_Target
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
            #pragma vertex BarkDepthVert
            #pragma fragment BarkDepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _USE_VERTEX_COLOR_WIND

            struct DepthAttr { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DepthVary { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            DepthVary BarkDepthVert(DepthAttr input)
            {
                DepthVary o;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                #ifdef _USE_VERTEX_COLOR_WIND
                    half windMask = input.color.r;
                #else
                    half windMask = saturate(input.positionOS.y);
                #endif

                half sway = ComputeBarkSway(posWS, _SwaySpeed, _SwayFrequency, _Time.y);
                posWS.x += sway * _SwayStrength * windMask;

                o.positionCS = TransformWorldToHClip(posWS);

                #ifdef _ALPHATEST_ON
                    o.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                #else
                    o.uv = 0;
                #endif
                return o;
            }

            half4 BarkDepthFrag(DepthVary input) : SV_Target
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
