Shader "Universal Render Pipeline/Custom/TriplanarGridLayered_FullLit_Fix"
{
    Properties
    {
        [Header(Base Settings)]
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _BaseMapScale("Base Map Scale", Float) = 1.0

        [Header(Grid Settings)]
        _GridColor("Grid Color", Color) = (1, 1, 1, 1)
        _GridMap("Grid Map", 2D) = "black" {}
        _GridMapScale("Grid Map Scale", Float) = 1.0

        [Header(Triplanar Settings)]
        _TriplanarBlend("Triplanar Blend", Range(0.01, 20.0)) = 4.0

        [Header(Surface Settings)]
        _Cutoff("Alpha Clipping", Range(0.0, 1.0)) = 0.5
        _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 1.0)
        _SpecGlossMap("Specular Map", 2D) = "white" {}
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5

        [Header(Normal Settings)]
        _BumpScale("Normal Scale", Float) = 1.0
        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}

        [Header(Emission Settings)]
        [Toggle] _UseEmission("Enable Emission", Float) = 0.0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0)
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}

        [Header(Advanced)]
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1.0
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half _BaseMapScale;
        half4 _GridColor;
        half _GridMapScale;
        half4 _SpecColor;
        half4 _EmissionColor;
        half _Cutoff;
        half _TriplanarBlend;
        half _Smoothness;
        half _SpecularHighlights;
        half _BumpScale;
        half _ReceiveShadows;
        half _UseEmission;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_GridMap); SAMPLER(sampler_GridMap);
        TEXTURE2D(_SpecGlossMap); SAMPLER(sampler_SpecGlossMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        float3 GetTriplanarWeights(float3 normalWS, float blendSharpness)
        {
            float3 weights = abs(normalWS);
            weights = pow(weights, blendSharpness);
            return weights / (dot(weights, 1.0) + 1e-6);
        }

        half4 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 weights, float3 positionWS, float scale)
        {
            float2 uvX = positionWS.zy * scale;
            float2 uvY = positionWS.xz * scale;
            float2 uvZ = positionWS.xy * scale;

            half4 colX = SAMPLE_TEXTURE2D(tex, samp, uvX);
            half4 colY = SAMPLE_TEXTURE2D(tex, samp, uvY);
            half4 colZ = SAMPLE_TEXTURE2D(tex, samp, uvZ);

            return colX * weights.x + colY * weights.y + colZ * weights.z;
        }

        half3 UnpackNormalTriplanar(TEXTURE2D_PARAM(bumpMap, samp), float3 weights, float3 positionWS, float3 normalWS, float scale, float bumpScale)
        {
            float2 uvX = positionWS.zy * scale;
            float2 uvY = positionWS.xz * scale;
            float2 uvZ = positionWS.xy * scale;

            half4 packedNormalX = SAMPLE_TEXTURE2D(bumpMap, samp, uvX);
            half4 packedNormalY = SAMPLE_TEXTURE2D(bumpMap, samp, uvY);
            half4 packedNormalZ = SAMPLE_TEXTURE2D(bumpMap, samp, uvZ);

            half3 tangentNormalX = UnpackNormalScale(packedNormalX, bumpScale);
            half3 tangentNormalY = UnpackNormalScale(packedNormalY, bumpScale);
            half3 tangentNormalZ = UnpackNormalScale(packedNormalZ, bumpScale);

            half3 axisSign = sign(normalWS);
            tangentNormalX.z *= axisSign.x;
            tangentNormalY.z *= axisSign.y;
            tangentNormalZ.z *= axisSign.z;

            return normalize(
                tangentNormalX.zyx * weights.x +
                tangentNormalY.xzy * weights.y +
                tangentNormalZ.xyz * weights.z
            );
        }

        void AlphaClip(half alpha)
        {
            #if defined(_ALPHATEST_ON)
            clip(alpha - _Cutoff);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _SPECGLOSSMAP
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS

            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float fogFactor : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 vertexLight : TEXCOORD5;
                #endif
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_ADDITIONAL_LIGHT_SHADOWS) || defined(_SHADOWS_SOFT)
                float4 shadowCoord : TEXCOORD6;
                #endif
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = vertexInput.positionWS;
                output.positionCS = vertexInput.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);

                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                output.vertexLight = VertexLighting(output.positionWS, output.normalWS);
                #endif

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_ADDITIONAL_LIGHT_SHADOWS) || defined(_SHADOWS_SOFT)
                output.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normalWS = normalize(input.normalWS);
                float3 weights = GetTriplanarWeights(normalWS, _TriplanarBlend);

                half4 baseTex = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), weights, input.positionWS, _BaseMapScale);
                half4 gridTex = SampleTriplanar(TEXTURE2D_ARGS(_GridMap, sampler_GridMap), weights, input.positionWS, _GridMapScale);

                AlphaClip(baseTex.a * _BaseColor.a);

                #if defined(_NORMALMAP)
                normalWS = UnpackNormalTriplanar(TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), weights, input.positionWS, normalWS, _BaseMapScale, _BumpScale);
                #endif

                half3 finalBase = baseTex.rgb * _BaseColor.rgb;
                half3 finalGrid = gridTex.rgb * _GridColor.rgb;
                half3 albedo = lerp(finalBase, finalGrid, gridTex.a * _GridColor.a);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = 0;
                surfaceData.occlusion = 1.0;
                
                #ifdef _SPECGLOSSMAP
                half4 specGloss = SampleTriplanar(TEXTURE2D_ARGS(_SpecGlossMap, sampler_SpecGlossMap), weights, input.positionWS, _BaseMapScale);
                surfaceData.specular = specGloss.rgb * _SpecColor.rgb;
                surfaceData.smoothness = specGloss.a * _Smoothness;
                #else
                surfaceData.specular = _SpecColor.rgb;
                surfaceData.smoothness = _Smoothness;
                #endif

                surfaceData.emission = 0;
                #ifdef _EMISSION
                if (_UseEmission > 0.5)
                {
                    half3 emissionTex = SampleTriplanar(TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap), weights, input.positionWS, _BaseMapScale).rgb;
                    surfaceData.emission = emissionTex * _EmissionColor.rgb;
                }
                #endif

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceViewDir(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = 0;
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
                
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                inputData.vertexLighting = input.vertexLight;
                #endif
                
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_ADDITIONAL_LIGHT_SHADOWS) || defined(_SHADOWS_SOFT)
                inputData.shadowCoord = input.shadowCoord;
                #endif

                return UniversalFragmentBlinnPhong(inputData, surfaceData);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #if defined(_ALPHATEST_ON)
                float3 weights = GetTriplanarWeights(normalize(input.normalWS), _TriplanarBlend);
                half4 baseTex = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), weights, input.positionWS, _BaseMapScale);
                AlphaClip(baseTex.a * _BaseColor.a);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #if defined(_ALPHATEST_ON)
                float3 weights = GetTriplanarWeights(normalize(input.normalWS), _TriplanarBlend);
                half4 baseTex = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), weights, input.positionWS, _BaseMapScale);
                AlphaClip(baseTex.a * _BaseColor.a);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile_instancing
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                float3 weights = GetTriplanarWeights(normalWS, _TriplanarBlend);

                #if defined(_ALPHATEST_ON)
                half4 baseTex = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), weights, input.positionWS, _BaseMapScale);
                AlphaClip(baseTex.a * _BaseColor.a);
                #endif

                #if defined(_NORMALMAP)
                normalWS = UnpackNormalTriplanar(TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), weights, input.positionWS, normalWS, _BaseMapScale, _BumpScale);
                #endif
                
                return float4(NormalizeNormalPerPixel(normalWS), 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaSimple
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _SPECGLOSSMAP
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitMetaPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "TriplanarGridShaderGUI"
    Fallback "Universal Render Pipeline/Lit"
}