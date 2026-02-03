Shader "Custom/Eye/Anime Parallax Dynamic Layers (Optimized)"
{
    Properties
    {
        [Header(Base)]
        _MainTex ("Iris Texture", 2D) = "white"{}
        _Color ("Iris Tint", Color) = (1, 1, 1, 1)

        [Header(Parallax Global)]
        _ParallaxStrength ("Global Parallax Strength", Range(0, 0.1)) = 0.02
        _ParallaxCenter ("Parallax Center UV", Vector) = (0.5, 0.5, 0, 0)

        [Header(Eye Socket)]
        [Toggle(_USESOCKET_ON)] _UseEyeSocket ("Enable Eye Socket Depth", Float) = 1
        _EyeSocketMask ("Eye Socket Mask (R)", 2D) = "black"{}
        _EyeSocketDepth ("Eye Socket Depth", Range(0, 0.2)) = 0.05

        [Header(Rim Light)]
        _RimPower ("Rim Power", Range(0.1, 10)) = 3
        _RimColor ("Rim Color", Color) = (1, 1, 1, 0.5)

        [Header(Dynamic Layers)]
        [Toggle(_LAYER1_ON)] _Layer1_On ("[1] Enable", Float) = 0
        _Layer1_Tex ("[1] Texture", 2D) = "white"{}
        _Layer1_Tint ("[1] Tint", Color) = (1, 1, 1, 1)
        [Enum(AlphaBlend, 0, Additive, 1, Multiply, 2)] _Layer1_BlendMode ("[1] Blend Mode", Float) = 0
        _Layer1_Depth ("[1] Parallax Depth", Range(-0.1, 0.1)) = 0.01
        _Layer1_Emissive ("[1] Emissive Strength", Range(0, 10)) = 0
        _Layer1_Offset ("[1] UV Offset", Vector) = (0, 0, 0, 0)
        _Layer1_ScrollSpeed ("[1] Scroll Speed", Vector) = (0, 0, 0, 0)
        _Layer1_Scale ("[1] Scale", Vector) = (1, 1, 0, 0)
        _Layer1_Rotation ("[1] Rotation", Range(0, 360)) = 0

        [Toggle(_LAYER2_ON)] _Layer2_On ("[2] Enable", Float) = 0
        _Layer2_Tex ("[2] Texture", 2D) = "white"{}
        _Layer2_Tint ("[2] Tint", Color) = (1, 1, 1, 1)
        [Enum(AlphaBlend, 0, Additive, 1, Multiply, 2)] _Layer2_BlendMode ("[2] Blend Mode", Float) = 0
        _Layer2_Depth ("[2] Parallax Depth", Range(-0.1, 0.1)) = 0.02
        _Layer2_Emissive ("[2] Emissive Strength", Range(0, 10)) = 0
        _Layer2_Offset ("[2] UV Offset", Vector) = (0, 0, 0, 0)
        _Layer2_ScrollSpeed ("[2] Scroll Speed", Vector) = (0, 0, 0, 0)
        _Layer2_Scale ("[2] Scale", Vector) = (1, 1, 0, 0)
        _Layer2_Rotation ("[2] Rotation", Range(0, 360)) = 0

        [Toggle(_LAYER3_ON)] _Layer3_On ("[3] Enable", Float) = 0
        _Layer3_Tex ("[3] Texture", 2D) = "white"{}
        _Layer3_Tint ("[3] Tint", Color) = (1, 1, 1, 1)
        [Enum(AlphaBlend, 0, Additive, 1, Multiply, 2)] _Layer3_BlendMode ("[3] Blend Mode", Float) = 1
        _Layer3_Depth ("[3] Parallax Depth", Range(-0.1, 0.1)) = 0.0
        _Layer3_Emissive ("[3] Emissive Strength", Range(0, 10)) = 1
        _Layer3_Offset ("[3] UV Offset", Vector) = (0, 0, 0, 0)
        _Layer3_ScrollSpeed ("[3] Scroll Speed", Vector) = (0, 0, 0, 0)
        _Layer3_Scale ("[3] Scale", Vector) = (1, 1, 0, 0)
        _Layer3_Rotation ("[3] Rotation", Range(0, 360)) = 0

        [Toggle(_LAYER4_ON)] _Layer4_On ("[4] Enable", Float) = 0
        _Layer4_Tex ("[4] Texture", 2D) = "white"{}
        _Layer4_Tint ("[4] Tint", Color) = (1, 1, 1, 1)
        [Enum(AlphaBlend, 0, Additive, 1, Multiply, 2)] _Layer4_BlendMode ("[4] Blend Mode", Float) = 2
        _Layer4_Depth ("[4] Parallax Depth", Range(-0.1, 0.1)) = -0.01
        _Layer4_Emissive ("[4] Emissive Strength", Range(0, 10)) = 0
        _Layer4_Offset ("[4] UV Offset", Vector) = (0, 0, 0, 0)
        _Layer4_ScrollSpeed ("[4] Scroll Speed", Vector) = (0, 0, 0, 0)
        _Layer4_Scale ("[4] Scale", Vector) = (1, 1, 0, 0)
        _Layer4_Rotation ("[4] Rotation", Range(0, 360)) = 0

        [Toggle(_LAYER5_ON)] _Layer5_On ("[5] Enable", Float) = 0
        _Layer5_Tex ("[5] Texture", 2D) = "white"{}
        _Layer5_Tint ("[5] Tint", Color) = (1, 0.2, 0.2, 1)
        [Enum(AlphaBlend, 0, Additive, 1, Multiply, 2)] _Layer5_BlendMode ("[5] Blend Mode", Float) = 0
        _Layer5_Depth ("[5] Parallax Depth", Range(-0.1, 0.1)) = 0.005
        _Layer5_Emissive ("[5] Emissive Strength", Range(0, 5)) = 0.5
        _Layer5_Offset ("[5] UV Offset", Vector) = (0, 0, 0, 0)
        _Layer5_ScrollSpeed ("[5] Scroll Speed", Vector) = (0, 0, 0, 0)
        _Layer5_Scale ("[5] Scale", Vector) = (1, 1, 0, 0)
        _Layer5_Rotation ("[5] Rotation", Range(0, 360)) = 0

        [Toggle(_LAYER6_ON)] _Layer6_On ("[6] Enable", Float) = 0
        _Layer6_Tex ("[6] Texture", 2D) = "white"{}
        _Layer6_Tint ("[6] Tint", Color) = (1, 1, 1, 1)
        [Enum(AlphaBlend, 0, Additive, 1, Multiply, 2)] _Layer6_BlendMode ("[6] Blend Mode", Float) = 0
        _Layer6_Depth ("[6] Parallax Depth", Range(-0.1, 0.1)) = 0.0
        _Layer6_Emissive ("[6] Emissive Strength", Range(0, 10)) = 0
        _Layer6_Offset ("[6] UV Offset", Vector) = (0, 0, 0, 0)
        _Layer6_ScrollSpeed ("[6] Scroll Speed", Vector) = (0, 0, 0, 0)
        _Layer6_Scale ("[6] Scale", Vector) = (1, 1, 0, 0)
        _Layer6_Rotation ("[6] Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "EyeForward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _LAYER1_ON
            #pragma shader_feature_local _LAYER2_ON
            #pragma shader_feature_local _LAYER3_ON
            #pragma shader_feature_local _LAYER4_ON
            #pragma shader_feature_local _LAYER5_ON
            #pragma shader_feature_local _LAYER6_ON
            #pragma shader_feature_local _USESOCKET_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color, _RimColor;
            half _ParallaxStrength, _RimPower, _EyeSocketDepth;
            float2 _ParallaxCenter;

            half4 _Layer1_Tint, _Layer2_Tint, _Layer3_Tint, _Layer4_Tint, _Layer5_Tint, _Layer6_Tint;
            half _Layer1_Depth, _Layer2_Depth, _Layer3_Depth, _Layer4_Depth, _Layer5_Depth, _Layer6_Depth;
            half _Layer1_Emissive, _Layer2_Emissive, _Layer3_Emissive, _Layer4_Emissive, _Layer5_Emissive, _Layer6_Emissive;
            half _Layer1_BlendMode, _Layer2_BlendMode, _Layer3_BlendMode, _Layer4_BlendMode, _Layer5_BlendMode, _Layer6_BlendMode;
            float4 _Layer1_Offset, _Layer2_Offset, _Layer3_Offset, _Layer4_Offset, _Layer5_Offset, _Layer6_Offset;
            float4 _Layer1_ScrollSpeed, _Layer2_ScrollSpeed, _Layer3_ScrollSpeed, _Layer4_ScrollSpeed, _Layer5_ScrollSpeed, _Layer6_ScrollSpeed;
            float4 _Layer1_Scale, _Layer2_Scale, _Layer3_Scale, _Layer4_Scale, _Layer5_Scale, _Layer6_Scale;
            half _Layer1_Rotation, _Layer2_Rotation, _Layer3_Rotation, _Layer4_Rotation, _Layer5_Rotation, _Layer6_Rotation;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_EyeSocketMask);
            SAMPLER(sampler_EyeSocketMask);
            TEXTURE2D(_Layer1_Tex);
            SAMPLER(sampler_Layer1_Tex);
            TEXTURE2D(_Layer2_Tex);
            SAMPLER(sampler_Layer2_Tex);
            TEXTURE2D(_Layer3_Tex);
            SAMPLER(sampler_Layer3_Tex);
            TEXTURE2D(_Layer4_Tex);
            SAMPLER(sampler_Layer4_Tex);
            TEXTURE2D(_Layer5_Tex);
            SAMPLER(sampler_Layer5_Tex);
            TEXTURE2D(_Layer6_Tex);
            SAMPLER(sampler_Layer6_Tex);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs norm = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = pos.positionCS;
                OUT.normalWS = norm.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(pos.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            float2 TransformUV(float2 uv, float2 offset, float2 scroll, float2 scale, half rotation)
            {
                uv += offset + scroll * _Time.y;
                uv -= 0.5;

                float rad = rotation * (PI / 180.0);
                float s, c;
                sincos(rad, s, c);
                float2x2 rotationMatrix = float2x2(c, -s, s, c);

                uv = mul(uv, rotationMatrix);
                uv /= scale;
                uv += 0.5;

                return uv;
            }

            void ProcessDynamicLayer(inout half4 finalColor, inout half3 emissive, float2 baseUV, float3 viewDirTS,
            TEXTURE2D_PARAM(tex, sampler_tex), half4 tint, half depth, half blendMode, half emissiveStrength,
            float2 offset, float2 scroll, float2 scale, half rotation)
            {
                float2 parallaxOffset = viewDirTS.xy * depth * _ParallaxStrength;
                float2 layerUV = baseUV + parallaxOffset;

                layerUV = TransformUV(layerUV, offset, scroll, scale, rotation);

                half4 layerTex = SAMPLE_TEXTURE2D(tex, sampler_tex, layerUV);
                half4 layerColor = layerTex * tint;

                if (blendMode < 0.5)    // AlphaBlend
                {
                    finalColor.rgb = lerp(finalColor.rgb, layerColor.rgb, layerColor.a);
                }
                else if (blendMode < 1.5) // Additive
                {
                    finalColor.rgb += layerColor.rgb * layerColor.a;
                }
                else    // Multiply
                {
                    finalColor.rgb = lerp(finalColor.rgb, finalColor.rgb * layerColor.rgb, layerColor.a);
                }

                emissive += layerColor.rgb * layerColor.a * emissiveStrength;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDirTS = normalize(TransformWorldToObjectDir(IN.viewDirWS));
                viewDirTS.y *= -1;

                float2 socketParallax = 0;
                #if defined(_USESOCKET_ON)
                    float socketMask = SAMPLE_TEXTURE2D(_EyeSocketMask, sampler_EyeSocketMask, IN.uv).r;
                    socketParallax = viewDirTS.xy * socketMask * _EyeSocketDepth * _ParallaxStrength;
                #endif

                float2 baseUV = IN.uv + socketParallax;

                half4 iris = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseUV) * _Color;
                half4 finalCol = iris;
                half3 emissive = 0;

                #if defined(_LAYER1_ON)
                    ProcessDynamicLayer(finalCol, emissive, baseUV, viewDirTS, TEXTURE2D_ARGS(_Layer1_Tex, sampler_Layer1_Tex),
                    _Layer1_Tint, _Layer1_Depth, _Layer1_BlendMode, _Layer1_Emissive,
                    _Layer1_Offset.xy, _Layer1_ScrollSpeed.xy, _Layer1_Scale.xy, _Layer1_Rotation);
                #endif
                #if defined(_LAYER2_ON)
                    ProcessDynamicLayer(finalCol, emissive, baseUV, viewDirTS, TEXTURE2D_ARGS(_Layer2_Tex, sampler_Layer2_Tex),
                    _Layer2_Tint, _Layer2_Depth, _Layer2_BlendMode, _Layer2_Emissive,
                    _Layer2_Offset.xy, _Layer2_ScrollSpeed.xy, _Layer2_Scale.xy, _Layer2_Rotation);
                #endif
                #if defined(_LAYER3_ON)
                    ProcessDynamicLayer(finalCol, emissive, baseUV, viewDirTS, TEXTURE2D_ARGS(_Layer3_Tex, sampler_Layer3_Tex),
                    _Layer3_Tint, _Layer3_Depth, _Layer3_BlendMode, _Layer3_Emissive,
                    _Layer3_Offset.xy, _Layer3_ScrollSpeed.xy, _Layer3_Scale.xy, _Layer3_Rotation);
                #endif
                #if defined(_LAYER4_ON)
                    ProcessDynamicLayer(finalCol, emissive, baseUV, viewDirTS, TEXTURE2D_ARGS(_Layer4_Tex, sampler_Layer4_Tex),
                    _Layer4_Tint, _Layer4_Depth, _Layer4_BlendMode, _Layer4_Emissive,
                    _Layer4_Offset.xy, _Layer4_ScrollSpeed.xy, _Layer4_Scale.xy, _Layer4_Rotation);
                #endif
                #if defined(_LAYER5_ON)
                    ProcessDynamicLayer(finalCol, emissive, baseUV, viewDirTS, TEXTURE2D_ARGS(_Layer5_Tex, sampler_Layer5_Tex),
                    _Layer5_Tint, _Layer5_Depth, _Layer5_BlendMode, _Layer5_Emissive,
                    _Layer5_Offset.xy, _Layer5_ScrollSpeed.xy, _Layer5_Scale.xy, _Layer5_Rotation);
                #endif
                #if defined(_LAYER6_ON)
                    ProcessDynamicLayer(finalCol, emissive, baseUV, viewDirTS, TEXTURE2D_ARGS(_Layer6_Tex, sampler_Layer6_Tex),
                    _Layer6_Tint, _Layer6_Depth, _Layer6_BlendMode, _Layer6_Emissive,
                    _Layer6_Offset.xy, _Layer6_ScrollSpeed.xy, _Layer6_Scale.xy, _Layer6_Rotation);
                #endif

                half rim = 1.0 - saturate(dot(normalize(IN.viewDirWS), IN.normalWS));
                rim = pow(rim, _RimPower);
                finalCol.rgb += rim * _RimColor.rgb * _RimColor.a;

                finalCol.rgb += emissive;

                return half4(finalCol.rgb, iris.a);
            }
            ENDHLSL
        }
    }

    CustomEditor "EyeAnimeParallaxEditor"
}
