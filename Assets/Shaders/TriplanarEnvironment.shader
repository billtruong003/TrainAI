Shader "Custom/Toon/TriplanarEnvironment"
{
    Properties
    {
        [Header(Base Layer)]
        _MainTex ("Main Texture", 2D) = "gray" {}
        _Color ("Main Color", Color) = (0.8,0.8,0.8,1)
        _MainTiling ("Main Tiling", Float) = 0.5

        [Header(Smart Dirt Layer)]
        _NoiseTex ("Noise Texture (R)", 2D) = "white" {}
        _DirtColor ("Dirt Color", Color) = (0.4, 0.3, 0.2, 1)
        _DirtTiling ("Dirt Tiling", Float) = 0.5
        
        [Header(Smart Controls)]
        _EffectFactor ("Dirt Level (-2 to 2)", Range(-2, 2)) = 0.0
        _DirtBumpScale ("Dirt Bump Strength", Range(0, 2)) = 1.0
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 64)) = 8.0
        _EdgeSoftness ("Dirt Edge Softness", Range(0.01, 0.5)) = 0.1

        [Header(Lighting)]
        _RampThreshold ("Toon Ramp Threshold", Range(0, 1)) = 0.5
        _RampSmoothness ("Toon Ramp Smoothness", Range(0.001, 1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _DirtColor;
                float _MainTiling;
                float _DirtTiling;
                float _EffectFactor;
                float _DirtBumpScale;
                float _TriplanarSharpness;
                float _EdgeSoftness;
                float _RampThreshold;
                float _RampSmoothness;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float3 GetTriplanarWeights(float3 normal, float sharpness)
            {
                float3 weights = abs(normal);
                weights = pow(weights, sharpness);
                return weights / (weights.x + weights.y + weights.z);
            }

            float3 CalculateSurfaceGradient(float3 normal, float3 worldPos, float noiseValue, float bumpScale)
            {
                float dNx = ddx(noiseValue);
                float dNy = ddy(noiseValue);
                float3 sigmaX = ddx(worldPos);
                float3 sigmaY = ddy(worldPos);
                float3 r1 = cross(sigmaY, normal);
                float3 r2 = cross(normal, sigmaX);
                float det = dot(sigmaX, r1);
                float signDet = (det < 0.0) ? -1.0 : 1.0;
                // Fixed 1e-6 syntax to 0.000001
                float3 surfGrad = signDet * (dNx * r1 + dNy * r2) / (abs(det) + 0.000001);
                return surfGrad * bumpScale;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 weights = GetTriplanarWeights(normalWS, _TriplanarSharpness);
                float3 pos = input.positionWS;

                float3 uvMain = pos * _MainTiling;
                float4 colX = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvMain.yz);
                float4 colY = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvMain.xz);
                float4 colZ = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvMain.xy);
                float4 mainCol = colX * weights.x + colY * weights.y + colZ * weights.z;
                mainCol *= _Color;

                float3 uvDirt = pos * _DirtTiling;
                float nX = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvDirt.yz).r;
                float nY = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvDirt.xz).r;
                float nZ = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvDirt.xy).r;
                float noiseVal = nX * weights.x + nY * weights.y + nZ * weights.z;

                float threshold = 0.5 - (_EffectFactor * 0.5);
                float dirtMask = smoothstep(threshold - _EdgeSoftness, threshold + _EdgeSoftness, noiseVal);

                float3 surfGrad = float3(0,0,0);
                if (_DirtBumpScale > 0 && dirtMask > 0.01)
                {
                     surfGrad = CalculateSurfaceGradient(normalWS, input.positionWS, noiseVal, _DirtBumpScale * dirtMask);
                }
                float3 finalNormal = normalize(normalWS - surfGrad);

                float3 finalAlbedo = lerp(mainCol.rgb, _DirtColor.rgb, dirtMask);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = dot(finalNormal, mainLight.direction);
                float lightIntensity = smoothstep(_RampThreshold - _RampSmoothness, _RampThreshold + _RampSmoothness, NdotL * 0.5 + 0.5);
                float3 lighting = mainLight.color * (lightIntensity * mainLight.shadowAttenuation);
                float3 ambient = SampleSH(finalNormal);

                return float4(finalAlbedo * (lighting + ambient), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            // Manual Shadow Caster to ensure compilation
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            float3 _LightDirection;

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // Simple projection without bias dependency to guarantee compile
                // Ideally use ApplyShadowBias(positionWS, input.normalOS, _LightDirection)
                // if defined.
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }
            float4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            float4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            float4 frag(Varyings input) : SV_Target {
                // PackNormalOctRectEncode is standard URP/Core
                return float4(PackNormalOctRectEncode(TransformWorldToViewDir(input.normalWS, true)), 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}