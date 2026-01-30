Shader "Sidekick/Toon_Stylized_3Tone_Optimized"
{
    Properties
    {
        [Header(Base Settings)]
        [MainTexture] [NoScaleOffset] _ColorMap("ColorTexture", 2D) = "white"{}

        [Header(Toon Lighting)]
        _ToonThreshold("Shadow Threshold", Range(-1, 1)) = 0.0
        _HighlightThreshold("Highlight Threshold", Range(-1, 1)) = 0.5
        _ToonSmoothness("Edge Smoothness", Range(0.001, 1)) = 0.05

        [Header(Toon Colors)]
        _ShadowTint("Shadow Tint", Color) = (0.4, 0.4, 0.5, 1.0)
        _MidtoneColor("Midtone Tint", Color) = (0.8, 0.8, 0.8, 1.0)

        [Header(Layers Masks)]
        [NoScaleOffset] _SkinMaskTexture("SkinMaskTexture", 2D) = "white"{}
        [NoScaleOffset] _DarkMaskTexture("DarkMaskTexture", 2D) = "white"{}
        [NoScaleOffset] _DirtMaskTexture("DirtMaskTexture", 2D) = "white"{}
        [NoScaleOffset] _CutsMaskTexture("CutsMaskTexture", 2D) = "white"{}
        [NoScaleOffset] _EyeEdgeMaskTexture("EyeEdgeMaskTexture", 2D) = "white"{}

        [Header(Layer Parameters)]
        _SkinColor("SkinColor", Color) = (0, 0, 0, 0)
        _SkinColorAmount("SkinColorAmount", Range(0, 1)) = 0

        _DarkAmount("DarkAmount", Range(0, 1)) = 0.5

        _DirtColor("DirtColor", Color) = (0, 0, 0, 0)
        _DirtAmount("DirtAmount", Range(0, 1)) = 0

        _CutsColor("CutsColor", Color) = (0, 0, 0, 0)
        _CutsAmount("CutsAmount", Range(0, 1)) = 0

        _EyelinerAmount("EyelinerAmount", Range(0, 1)) = 0

        [HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _ColorMap_TexelSize;
        half4 _SkinColor;
        half4 _DirtColor;
        half4 _CutsColor;
        half4 _ShadowTint;
        half4 _MidtoneColor;
        half _SkinColorAmount;
        half _DarkAmount;
        half _DirtAmount;
        half _CutsAmount;
        half _EyelinerAmount;
        half _ToonThreshold;
        half _HighlightThreshold;
        half _ToonSmoothness;
        CBUFFER_END

        TEXTURE2D(_ColorMap);
        SAMPLER(sampler_ColorMap);
        TEXTURE2D(_SkinMaskTexture);
        TEXTURE2D(_DarkMaskTexture);
        TEXTURE2D(_DirtMaskTexture);
        TEXTURE2D(_CutsMaskTexture);
        TEXTURE2D(_EyeEdgeMaskTexture);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
        };
        ENDHLSL

        Pass
        {
            Name "ToonLit3Tone"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D(_ColorMap, sampler_ColorMap, input.uv);

                half skinMask = SAMPLE_TEXTURE2D(_SkinMaskTexture, sampler_ColorMap, input.uv).r;
                half darkMask = SAMPLE_TEXTURE2D(_DarkMaskTexture, sampler_ColorMap, input.uv).r;
                half dirtMask = SAMPLE_TEXTURE2D(_DirtMaskTexture, sampler_ColorMap, input.uv).r;
                half cutsMask = SAMPLE_TEXTURE2D(_CutsMaskTexture, sampler_ColorMap, input.uv).r;
                half eyeMask = SAMPLE_TEXTURE2D(_EyeEdgeMaskTexture, sampler_ColorMap, input.uv).r;

                half darkFactor = lerp(1.0, darkMask, _DarkAmount);
                half3 albedo = baseCol.rgb * darkFactor;

                albedo = lerp(albedo, _DirtColor.rgb, dirtMask * _DirtAmount);
                albedo = lerp(albedo, _CutsColor.rgb, cutsMask * _CutsAmount);
                albedo = lerp(albedo, _SkinColor.rgb, skinMask * _SkinColorAmount);

                half eyeFactorRaw = clamp(1.0 - _EyelinerAmount, 0.0, 0.5);
                half eyeFactor = lerp(1.0, eyeFactorRaw, eyeMask);
                albedo *= eyeFactor;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 normalWS = normalize(input.normalWS);

                half ndotl = dot(normalWS, mainLight.direction);
                half lightIntensity = ndotl * mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                half shadowStep = smoothstep(_ToonThreshold - _ToonSmoothness, _ToonThreshold + _ToonSmoothness, lightIntensity);
                half highlightStep = smoothstep(_HighlightThreshold - _ToonSmoothness, _HighlightThreshold + _ToonSmoothness, lightIntensity);

                half3 colShadow = albedo * _ShadowTint.rgb;
                half3 colMidtone = albedo * _MidtoneColor.rgb;
                half3 colHighlight = albedo * mainLight.color;

                half3 finalColor = lerp(colShadow, colMidtone, shadowStep);
                finalColor = lerp(finalColor, colHighlight, highlightStep);

                #ifdef _ADDITIONAL_LIGHTS
                    uint pixelLightCount = GetAdditionalLightsCount();
                    for(uint lightIndex = 0;
                    lightIndex < pixelLightCount;
                    ++lightIndex)
                    {
                        Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                        half addNdotL = dot(normalWS, addLight.direction);
                        half addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                        half addIntensity = addNdotL * addAtten;

                        half addRamp = smoothstep(_ToonThreshold, _ToonThreshold + _ToonSmoothness, addIntensity);
                        finalColor += albedo * addLight.color * addRamp;
                    }
                #endif

                return half4(finalColor, baseCol.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(normalize(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    // CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.LitShader"
}
