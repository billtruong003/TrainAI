Shader "Custom/ArenaShield"
{
    Properties
    {
        [Header(Visual Settings)]
        [MainColor] _BaseColor("Energy Color", Color) = (0, 0.8, 1, 0.5)
        _NoiseTex("Noise Texture", 2D) = "black" {}
        _NoiseStrength("Noise Intensity", Range(0, 5)) = 1.5
        
        [Header(Motion)]
        _ScrollSpeed("Scroll Speed (XY=Layer1, ZW=Layer2)", Vector) = (0.2, 0.4, -0.2, 0.1)
        
        [Header(Transparency Control)]
        _FresnelPower("View Clarity (Higher = Clearer Center)", Range(0.5, 8.0)) = 3.0
        _FresnelBoost("Edge Brightness", Range(1.0, 5.0)) = 2.0
        _DepthSoftness("Ground Blend Power", Range(0.1, 5.0)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ArenaShieldPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _NoiseTex_ST;
                float4 _ScrollSpeed;
                half _FresnelPower;
                half _FresnelBoost;
                half _NoiseStrength;
                float _DepthSoftness;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _NoiseTex);
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv1 = input.uv + _Time.y * _ScrollSpeed.xy;
                float2 uv2 = input.uv * 0.85 + _Time.y * _ScrollSpeed.zw;

                half noise1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv1).r;
                half noise2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv2).r;
                half electricity = pow(abs(noise1 * noise2), 0.8) * _NoiseStrength;

                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float3 normal = normalize(input.normalWS);
                half fresnelRaw = 1.0 - saturate(dot(viewDir, normal));
                half fresnel = pow(fresnelRaw, _FresnelPower) * _FresnelBoost;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float partDepth = LinearEyeDepth(input.screenPos.z / input.screenPos.w, _ZBufferParams);
                float depthFade = saturate((sceneDepth - partDepth) * _DepthSoftness);

                half4 finalColor;
                finalColor.rgb = _BaseColor.rgb * electricity * fresnel * depthFade;
                finalColor.a = _BaseColor.a * fresnel * depthFade;

                return finalColor;
            }
            ENDHLSL
        }
    }
}