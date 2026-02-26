#ifndef CLEAN_TOON_LIGHTING_INCLUDED
#define CLEAN_TOON_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

// ============================================================================
// Global Toon Style Parameters
// Set from ToonStyleConfig.cs via Shader.SetGlobal*()
// Wrapped in dedicated CBUFFER for SRP Batcher compatibility
// ============================================================================

CBUFFER_START(CleanRenderGlobals)
    half  _GlobalShadowThreshold;
    half  _GlobalShadowSmoothness;
    half4 _GlobalShadowColor;
    half4 _GlobalRimColor;
    half  _GlobalRimPower;
    half  _GlobalRimIntensity;
    half  _GlobalAmbientStrength;
    half  _GlobalSpecularCutoff;
    half  _GlobalSpecularSmoothness;
    half4 _GlobalSpecularColor;
CBUFFER_END

// ============================================================================
// Toon Ramp — cel shading with smoothstep
// Using half precision throughout to reduce VGPR pressure
// ============================================================================

half3 ToonRamp(half NdotL, half shadowAtten, half3 lightColor,
               half threshold, half smoothness, half3 shadowCol)
{
    half intensity = smoothstep(threshold - smoothness, threshold + smoothness, NdotL * shadowAtten);
    return lerp(shadowCol, lightColor, intensity);
}

half3 ToonRampGlobal(half NdotL, half shadowAtten, half3 lightColor)
{
    return ToonRamp(NdotL, shadowAtten, lightColor,
        _GlobalShadowThreshold, _GlobalShadowSmoothness, _GlobalShadowColor.rgb);
}

// ============================================================================
// Rim Light — view-dependent edge highlight
// Optimized: single smoothstep, precomputed inverse power
// ============================================================================

half3 ComputeRim(half3 normalWS, half3 viewDirWS, half lightIntensity,
                 half3 rimCol, half rimPow, half rimInt)
{
    half NdotV = 1.0h - saturate(dot(normalWS, viewDirWS));
    // Precompute threshold from power to avoid pow()
    half rimThreshold = 1.0h - rcp(rimPow);
    half rim = smoothstep(rimThreshold, 1.0h, NdotV * lightIntensity);
    return rimCol * (rim * rimInt);
}

half3 ComputeRimGlobal(half3 normalWS, half3 viewDirWS, half lightIntensity)
{
    return ComputeRim(normalWS, viewDirWS, lightIntensity,
        _GlobalRimColor.rgb, _GlobalRimPower, _GlobalRimIntensity);
}

// ============================================================================
// Toon Specular — hard cutoff specular highlight
// ============================================================================

half3 ToonSpecular(half3 normalWS, half3 lightDirWS, half3 viewDirWS,
                   half3 specCol, half cutoff, half smoothness, half shadowAtten)
{
    half3 halfDir = normalize(lightDirWS + viewDirWS);
    half NdotH = saturate(dot(normalWS, halfDir));
    half spec = smoothstep(cutoff - smoothness, cutoff + smoothness, NdotH) * shadowAtten;
    return specCol * spec;
}

half3 ToonSpecularGlobal(half3 normalWS, half3 lightDirWS, half3 viewDirWS, half shadowAtten)
{
    return ToonSpecular(normalWS, lightDirWS, viewDirWS,
        _GlobalSpecularColor.rgb, _GlobalSpecularCutoff, _GlobalSpecularSmoothness, shadowAtten);
}

// ============================================================================
// Metal Fresnel — view-dependent metallic highlight
// ============================================================================

half3 ToonMetalFresnel(half3 normalWS, half3 viewDirWS,
                       half3 baseColor, half3 metalColor,
                       half cutoff, half smoothness)
{
    half NdotV = dot(normalWS, viewDirWS);
    half fresnel = smoothstep(cutoff - smoothness, cutoff + smoothness, NdotV);
    return lerp(baseColor, metalColor, fresnel);
}

// ============================================================================
// Combined Toon Lighting Result
// ============================================================================

struct ToonLightResult
{
    half3 diffuse;
    half3 rim;
    half3 globalIllumination;
    half3 specular;
    half  lightIntensity;
};

ToonLightResult ComputeToonMainLight(
    float3 positionWS, half3 normalWS, half3 viewDirWS, half3 albedo,
    float2 lightmapUV,
    half threshold, half smoothness, half3 shadowCol,
    half3 rimCol, half rimPow, half rimInt, half ambientStr)
{
    ToonLightResult result = (ToonLightResult)0;

    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    half NdotL = saturate(dot(normalWS, (half3)mainLight.direction));
    half shadowAtten = (half)mainLight.shadowAttenuation;
    half intensity = smoothstep(threshold - smoothness, threshold + smoothness, NdotL * shadowAtten);

    #ifdef LIGHTMAP_ON
        half3 gi = SampleLightmap(lightmapUV, normalWS);
    #else
        half3 gi = SampleSH(normalWS);
    #endif

    result.lightIntensity = intensity;
    result.diffuse = albedo * lerp(shadowCol, (half3)mainLight.color, intensity);
    result.rim = ComputeRim(normalWS, viewDirWS, intensity, rimCol, rimPow, rimInt);
    result.globalIllumination = gi * albedo * ambientStr;
    result.specular = (half3)0;

    return result;
}

ToonLightResult ComputeToonMainLightGlobal(
    float3 positionWS, half3 normalWS, half3 viewDirWS,
    half3 albedo, float2 lightmapUV)
{
    return ComputeToonMainLight(
        positionWS, normalWS, viewDirWS, albedo, lightmapUV,
        _GlobalShadowThreshold, _GlobalShadowSmoothness, _GlobalShadowColor.rgb,
        _GlobalRimColor.rgb, _GlobalRimPower, _GlobalRimIntensity,
        _GlobalAmbientStrength);
}

// ============================================================================
// Additional Lights — toon-style point/spot lights
// Optimized: half precision, early-out for zero attenuation
// ============================================================================

half3 ComputeToonAdditionalLights(
    float3 positionWS, half3 normalWS, half3 albedo,
    half threshold, half smoothness)
{
    half3 result = (half3)0;

    #ifdef _ADDITIONAL_LIGHTS
    uint lightCount = GetAdditionalLightsCount();
    for (uint i = 0; i < lightCount; i++)
    {
        Light light = GetAdditionalLight(i, positionWS);
        half atten = (half)(light.distanceAttenuation * light.shadowAttenuation);

        // Early skip zero-contribution lights
        if (atten < 0.001h) continue;

        half NdotL = saturate(dot(normalWS, (half3)light.direction));
        half intensity = smoothstep(threshold - smoothness, threshold + smoothness, NdotL * atten);
        result += albedo * (half3)light.color * intensity;
    }
    #endif

    return result;
}

half3 ComputeToonAdditionalLightsGlobal(float3 positionWS, half3 normalWS, half3 albedo)
{
    return ComputeToonAdditionalLights(positionWS, normalWS, albedo,
        _GlobalShadowThreshold * 0.8h, _GlobalShadowSmoothness * 2.0h);
}

// ============================================================================
// Shadow Caster Helpers
// Shared across all shaders for consistent shadow behavior
// ============================================================================

struct ShadowCasterAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ShadowCasterVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Standard shadow caster vertex transform
// _LightDirection must be declared in the pass that uses this
float4 TransformShadowCasterPositionCS(float3 positionOS, float3 normalOS, float3 lightDirection)
{
    float3 posWS = TransformObjectToWorld(positionOS);
    float3 normWS = TransformObjectToWorldNormal(normalOS);
    float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, lightDirection));

    #if UNITY_REVERSED_Z
        posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif

    return posCS;
}

#endif // CLEAN_TOON_LIGHTING_INCLUDED
