Shader "CleanRender/ToonTerrain"
{
    Properties
    {
        [Header(Terrain Layers)]
        _Layer0("Layer 0 (Low)", 2D) = "white"{}
        _Layer0Color("Layer 0 Tint", Color) = (0.6, 0.55, 0.4, 1)
        _Layer1("Layer 1 (Mid)", 2D) = "white"{}
        _Layer1Color("Layer 1 Tint", Color) = (0.3, 0.55, 0.2, 1)
        _Layer2("Layer 2 (High)", 2D) = "white"{}
        _Layer2Color("Layer 2 Tint", Color) = (0.7, 0.7, 0.72, 1)
        _Layer3("Layer 3 (CliffTriplanar)", 2D) = "white"{}
        _Layer3Color("Layer 3 Tint", Color) = (0.45, 0.4, 0.35, 1)

        [Header(Height Blending)]
        _HeightLow("Height Low", Float) = 5
        _HeightMid("Height Mid", Float) = 20
        _BlendSharpness("Blend Sharpness", Range(0.1, 20)) = 5
        _HeightOffset("Height Offset", Float) = 0

        [Header(Triplanar Cliff)]
        _TriplanarScale("Triplanar Scale", Float) = 0.2
        _TriplanarSharpness("Triplanar Blend Sharpness", Range(1, 10)) = 4
        _CliffAngle("Cliff Angle Threshold", Range(0, 1)) = 0.5

        [Header(Texture Scale)]
        _TexScale("Global Texture Scale", Float) = 0.1

        [Header(Cel Shading)]
        _ShadowColor("Shadow Color", Color) = (0.2, 0.22, 0.3, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.45
        _Smoothness("Shadow Smoothness", Range(0.001, 0.5)) = 0.06
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex TerrainVert
            #pragma fragment TerrainFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/ToonLighting.hlsl"
            #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/NoiseLib.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Layer0_ST; half4 _Layer0Color;
                float4 _Layer1_ST; half4 _Layer1Color;
                float4 _Layer2_ST; half4 _Layer2Color;
                float4 _Layer3_ST; half4 _Layer3Color;
                float _HeightLow; float _HeightMid;
                float _BlendSharpness; float _HeightOffset;
                float _TriplanarScale; float _TriplanarSharpness;
                float _CliffAngle; float _TexScale;
                half4 _ShadowColor; float _Threshold; float _Smoothness;
            CBUFFER_END

            TEXTURE2D(_Layer0); SAMPLER(sampler_Layer0);
            TEXTURE2D(_Layer1); SAMPLER(sampler_Layer1);
            TEXTURE2D(_Layer2); SAMPLER(sampler_Layer2);
            TEXTURE2D(_Layer3); SAMPLER(sampler_Layer3);

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
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings TerrainVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                return o;
            }

            half4 TerrainFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 N = normalize(input.normalWS);
                float height = input.positionWS.y - _HeightOffset;

                float w0 = saturate(1.0 - saturate((height - _HeightLow) * _BlendSharpness * 0.1));
                float w2 = saturate((height - _HeightMid) * _BlendSharpness * 0.1);
                float w1 = max(1.0 - w0 - w2, 0.0);

                float cliffMask = 1.0 - saturate((N.y - _CliffAngle) / (1.0 - _CliffAngle + 0.01));

                float2 worldUV = input.positionWS.xz * _TexScale;
                half3 c0 = SAMPLE_TEXTURE2D(_Layer0, sampler_Layer0, worldUV).rgb * _Layer0Color.rgb;
                half3 c1 = SAMPLE_TEXTURE2D(_Layer1, sampler_Layer1, worldUV).rgb * _Layer1Color.rgb;
                half3 c2 = SAMPLE_TEXTURE2D(_Layer2, sampler_Layer2, worldUV).rgb * _Layer2Color.rgb;

                TriplanarUV tp = ComputeTriplanarUV(input.positionWS, N, _TriplanarScale, _TriplanarSharpness);
                half3 c3 = SampleTriplanar(TEXTURE2D_ARGS(_Layer3, sampler_Layer3), tp).rgb * _Layer3Color.rgb;

                half3 flatColor = c0 * w0 + c1 * w1 + c2 * w2;
                half3 albedo = lerp(flatColor, c3, cliffMask);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = dot(N, mainLight.direction);
                float intensity = smoothstep(_Threshold - _Smoothness, _Threshold + _Smoothness, NdotL * mainLight.shadowAttenuation);
                half3 litColor = albedo * lerp(_ShadowColor.rgb, mainLight.color, intensity);

                litColor += SampleSH(N) * albedo * 0.3;

                return half4(litColor, 1);
            }
            ENDHLSL
        }

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                return half4(n * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On 
            ZTest LEqual 
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex SV
            #pragma fragment SF
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            float3 _LightDirection;
            
            struct A 
            { 
                float4 p : POSITION; 
                float3 n : NORMAL; 
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };
            
            struct V 
            { 
                float4 p : SV_POSITION; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            V SV(A i) 
            { 
                V o = (V)0; 
                UNITY_SETUP_INSTANCE_ID(i); 
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                float3 ws = TransformObjectToWorld(i.p.xyz); 
                float3 wn = TransformObjectToWorldNormal(i.n); 
                o.p = TransformWorldToHClip(ApplyShadowBias(ws, wn, _LightDirection)); 
                return o; 
            }
            
            half4 SF(V i) : SV_Target 
            { 
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return 0; 
            }
            ENDHLSL
        }
    }
}
