Shader "Custom/ToonComplete"
{
    Properties
    {
        [Header(Base Settings)]
        _BaseMap("Base Map", 2D) = "white"{}
        [HDR] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Lighting)]
        _RampTex("Toon Ramp (Gradient)", 2D) = "white"{}
        _RampThreshold("Ramp Threshold", Range(0, 1)) = 0.5
        _RampSmooth("Ramp Smoothness", Range(0.001, 1)) = 0.1

        [Header(Specular)]
        [Toggle(_SPECULAR_ON)] _UseSpecular("Enable Specular", Float) = 0
        _SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularSize("Specular Size", Range(0, 1)) = 0.1
        _SpecularFalloff("Specular Falloff", Range(0.001, 1)) = 0.1

        [Header(Rim Light)]
        [Toggle(_RIM_ON)] _UseRim("Enable Rim Light", Float) = 0
        _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(0.1, 10)) = 3.0
        _RimThreshold("Rim Threshold", Range(0, 1)) = 0.5

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Range(0, 0.05)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ------------------------------------------------------------------
        // PASS 1: MAIN LIGHTING (TOON)
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Shader Features
            #pragma shader_feature_local _SPECULAR_ON
            #pragma shader_feature_local _RIM_ON

            // URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float fogFactor : TEXCOORD5;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _SpecularColor;
            float _SpecularSize;
            float _SpecularFalloff;
            float4 _RimColor;
            float _RimPower;
            float _RimThreshold;
            float _RampThreshold;
            float _RampSmooth;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RampTex);
            SAMPLER(sampler_RampTex);

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Setup Data
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(input.shadowCoord);

                // 2. Base Color & Texture
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = baseMap.rgb * _BaseColor.rgb;

                // 3. Toon Lighting (NdotL)
                float NdotL = dot(normalWS, mainLight.direction);
                // Shadow Attenuation (Shadow map)
                float shadowAtten = mainLight.shadowAttenuation;

                // --- Ramp Calculation ---
                // Mapping NdotL (-1 to 1) to UV coordinates (0 to 1)
                float halfLambert = NdotL * 0.5 + 0.5;

                // Apply shadow to the lookup to make shadows affect the ramp
                float rampUV = halfLambert * shadowAtten;

                // Sample Ramp Texture (If texture exists) or use Math fallback
                half3 rampColor;
                // Simple Math-based ramp logic if you prefer procedural control:
                float rampIntensity = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, rampUV);
                // Or Texture Sample:
                half4 rampTexSample = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(rampUV, 0.5));

                // Combine light color, ramp, and intensity
                half3 lighting = mainLight.color * rampTexSample.rgb; // Use ramp texture color

                // 4. Specular (Toon Style)
                half3 specular = 0;
                #ifdef _SPECULAR_ON
                    float3 halfDir = normalize(mainLight.direction + viewDirWS);
                    float NdotH = dot(normalWS, halfDir);
                    float specIntensity = smoothstep(1 - _SpecularSize, 1 - _SpecularSize + _SpecularFalloff, NdotH);
                    specular = _SpecularColor.rgb * specIntensity * shadowAtten;
                #endif

                // 5. Rim Light
                half3 rim = 0;
                #ifdef _RIM_ON
                    float NdotV = 1.0 - saturate(dot(normalWS, viewDirWS));
                    float rimIntensity = smoothstep(_RimThreshold - 0.1, _RimThreshold + 0.1, pow(NdotV, _RimPower));
                    // Rim only appears on lit side (optional) or everywhere.
                    rim = _RimColor.rgb * rimIntensity * mainLight.color;
                #endif

                // 6. Ambient (Environment)
                half3 ambient = SampleSH(normalWS);

                // 7. Final Combine
                half3 finalColor = albedo * (lighting + ambient) + specular + rim;

                // Apply Fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // PASS 2: OUTLINE (INVERTED HULL)
        // ------------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }

            Cull Front  // Draw backfaces only
            ZWrite On
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _OutlineColor;
            float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Extrude vertices along normal
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // Scale width based on distance (optional, keeps outline consistent)
                // For simple toon, constant width in object space is often fine:
                float3 extrudedPositionOS = input.positionOS.xyz + input.normalOS * _OutlineWidth;

                output.positionCS = TransformObjectToHClip(extrudedPositionOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // PASS 3: SHADOW CASTER
        // ------------------------------------------------------------------
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

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                float3 positionWS = vertexInput.positionWS;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                output.uv = input.uv;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    CustomEditor "ToonShaderGUI"
}
