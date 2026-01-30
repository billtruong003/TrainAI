Shader "RobotAI/LaserBeam"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Intensity", Color) = (2, 0, 0, 1)
        _MainTex("Beam Texture", 2D) = "white"{}
        _ScrollSpeed("Scroll Speed", Float) = 3.0
        _FresnelPower("Fresnel Power", Range(0.1, 10.0)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "LaserPass"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 normalWS : NORMAL;
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EmissionColor;
            float4 _MainTex_ST;
            float _ScrollSpeed;
            float _FresnelPower;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                // Safe normal transformation without requiring tangents
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Texture Scrolling
                float2 scrollUV = input.uv;
                scrollUV.x -= _Time.y * _ScrollSpeed;

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrollUV);

                // Fresnel Effect
                float3 viewDir = normalize(input.viewDirWS);
                float3 normal = normalize(input.normalWS);
                float NdotV = saturate(dot(viewDir, normal));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // Color Composition
                half3 finalColor = texColor.rgb * _BaseColor.rgb * _EmissionColor.rgb;

                // Alpha Calculation: Texture Alpha * Base Alpha * (Fresnel Enhancement)
                half alpha = texColor.a * _BaseColor.a * saturate(0.2 + fresnel);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
