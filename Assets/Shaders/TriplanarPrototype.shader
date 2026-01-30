Shader "Custom/Toon/TriplanarPrototype"
{
    Properties
    {
        [Header(Base Settings)]
        _BaseTex ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.2, 0.2, 0.2, 1)
        _BaseTiling ("Base Tiling", Float) = 1.0
        
        [Header(Smart Grid)]
        _GridColor ("Grid Color", Color) = (0.5, 0.5, 0.5, 1)
        _GridSpacing ("Grid Spacing", Float) = 1.0
        _GridThickness ("Grid Thickness", Range(0.01, 0.2)) = 0.02
        _GridFalloff ("Grid Falloff (Distance)", Float) = 50.0

        [Header(Logo Settings)]
        _LogoTex ("Logo Texture", 2D) = "black" {}
        _LogoColor ("Logo Color", Color) = (1, 1, 1, 1)
        _LogoScale ("Logo Scale", Float) = 1.0

        [Header(Triplanar)]
        _BlendSharpness ("Blend Sharpness", Range(1, 20)) = 10.0
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
                float4 _BaseColor;
                float4 _GridColor;
                float4 _LogoColor;
                float _BaseTiling;
                float _GridSpacing;
                float _GridThickness;
                float _GridFalloff;
                float _LogoScale;
                float _BlendSharpness;
            CBUFFER_END

            TEXTURE2D(_BaseTex); SAMPLER(sampler_BaseTex);
            TEXTURE2D(_LogoTex); SAMPLER(sampler_LogoTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 weights = abs(normalWS);
                weights = pow(weights, _BlendSharpness);
                weights /= (weights.x + weights.y + weights.z);

                float3 p = input.positionWS * _BaseTiling;
                float4 bx = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, p.yz);
                float4 by = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, p.xz);
                float4 bz = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, p.xy);
                float4 baseTexCol = bx * weights.x + by * weights.y + bz * weights.z;
                float3 finalColor = baseTexCol.rgb * _BaseColor.rgb;

                if (_GridSpacing > 0)
                {
                    float3 pGrid = input.positionWS;
                    float3 gridUV = abs(frac(pGrid / _GridSpacing) - 0.5);
                    float3 gridDeriv = fwidth(pGrid / _GridSpacing);
                    float3 gridLine = smoothstep(0.5 - _GridThickness, 0.5, gridUV);
                    gridLine = 1.0 - gridLine;
                    float gX = max(gridLine.y, gridLine.z);
                    float gY = max(gridLine.x, gridLine.z);
                    float gZ = max(gridLine.x, gridLine.y);
                    float finalGrid = gX * weights.x + gY * weights.y + gZ * weights.z;
                    float dist = distance(input.positionWS, _WorldSpaceCameraPos);
                    float fade = 1.0 - smoothstep(_GridFalloff * 0.5, _GridFalloff, dist);
                    finalColor = lerp(finalColor, _GridColor.rgb, finalGrid * fade * _GridColor.a);
                }

                if (normalWS.y > 0.0)
                {
                     float2 logoUV = input.positionWS.xz * _LogoScale;
                     float4 logo = SAMPLE_TEXTURE2D(_LogoTex, sampler_LogoTex, logoUV);
                     float logoMask = smoothstep(0.8, 1.0, normalWS.y);
                     finalColor = lerp(finalColor, _LogoColor.rgb * logo.rgb, logo.a * logoMask * _LogoColor.a);
                }

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 lighting = mainLight.color * (NdotL * mainLight.shadowAttenuation);
                float3 ambient = SampleSH(normalWS);

                return float4(finalColor * (lighting + ambient), 1.0);
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
            
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
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
                return float4(PackNormalOctRectEncode(TransformWorldToViewDir(input.normalWS, true)), 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}