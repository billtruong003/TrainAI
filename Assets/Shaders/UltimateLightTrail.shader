Shader "Universal Render Pipeline/VFX/UltimateLightTrail"
{
    Properties
    {
        [MainColor] [HDR] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _MainTex("Main Texture", 2D) = "white" {}
        _MainScroll("Main Scroll Speed (UV)", Vector) = (0, 0, 0, 0)
        
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _NoiseScroll("Noise Scroll Speed (UV)", Vector) = (0.5, 0, 0, 0)
        _NoiseStrength("Noise Strength", Range(0, 2)) = 1

        _Softness("Soft Particle Factor", Range(0.01, 10.0)) = 1.0
        _NearFadeDistance("Camera Near Fade", Range(0, 5)) = 0.5
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

        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        ColorMask RGB

        Pass
        {
            Name "LightTrailPass"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float4 screenPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float2 _MainScroll;
                float2 _NoiseScroll;
                half _NoiseStrength;
                float _Softness;
                float _NearFadeDistance;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 mainUV = TRANSFORM_TEX(input.uv, _MainTex) + (_Time.y * _MainScroll);
                float2 noiseUV = TRANSFORM_TEX(input.uv, _NoiseTex) + (_Time.y * _NoiseScroll);

                half4 mainTexColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);
                half4 noiseTexColor = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV);

                half noiseFactor = lerp(1.0, noiseTexColor.r, _NoiseStrength);
                half4 finalColor = mainTexColor * noiseFactor * _BaseColor * input.color;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);
                float partZ = input.screenPos.w;
                
                float fadeDepth = saturate((sceneZ - partZ) * _Softness);
                float fadeCamera = saturate((partZ - _NearFadeDistance));

                finalColor.a *= fadeDepth * fadeCamera;
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}