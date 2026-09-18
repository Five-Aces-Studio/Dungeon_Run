#ifndef DUNGEON_RUN_STYLIZED_FORWARD_INCLUDED
#define DUNGEON_RUN_STYLIZED_FORWARD_INCLUDED
#include "DungeonRunStylizedInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
    half3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    half fogFactor : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings StylizedVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.fogFactor = ComputeFogFactor(position.positionCS.z);
    return output;
}

half BandStep(float threshold, float value)
{
    if (_BandSoftness <= 0.00001) return step(threshold, value);
    return smoothstep(threshold - _BandSoftness, threshold + _BandSoftness, value);
}

void AccumulateLight(Light light, half3 normalWS, half3 viewWS,
                     inout float3 diffuse, inout float3 specular)
{
    half ndotl = saturate(dot(normalWS, light.direction));
    float3 radiance = light.color * light.distanceAttenuation * light.shadowAttenuation;
    diffuse += radiance * ndotl;
    half3 halfDirection = SafeNormalize(light.direction + viewWS);
    half lobe = pow(saturate(dot(normalWS, halfDirection)), lerp(12.0, 64.0, _Smoothness));
    half highlight = BandStep(_SpecularThreshold, lobe);
    specular += radiance * ndotl * highlight;
}

half4 StylizedFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    inputData.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    half4 shadowMask = half4(1, 1, 1, 1);
    float3 direct = 0;
    float3 specular = 0;
    Light mainLight = GetMainLight(inputData.shadowCoord, input.positionWS, shadowMask);
    AccumulateLight(mainLight, inputData.normalWS, inputData.viewDirectionWS, direct, specular);

#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
    float3 additionalDiffuse = 0;
    float3 additionalSpecular = 0;
    // URP 17.3: clustered directionals are separate from the punctual cluster iterator.
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
        AccumulateLight(light, inputData.normalWS, inputData.viewDirectionWS, additionalDiffuse, additionalSpecular);
    }
#endif
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
        AccumulateLight(light, inputData.normalWS, inputData.viewDirectionWS, additionalDiffuse, additionalSpecular);
    LIGHT_LOOP_END
    direct += additionalDiffuse * _AdditionalLightStrength;
    specular += additionalSpecular * _AdditionalLightStrength;
#endif

    // Quantize combined irradiance once; retain relative warm/cool light color.
    float intensity = max(direct.r, max(direct.g, direct.b));
    // In complete shadow there is no direct-light hue to normalize. Blend to
    // neutral at zero so the lowest band retains its authored shadow tint;
    // above the first threshold the actual combined light hue is unchanged.
    half chromaticWeight = saturate(intensity / max(_BandThresholds.x, 0.0001));
    half3 hue = lerp(half3(1, 1, 1), direct / max(intensity, 0.0001), chromaticWeight);
    half3 weights = half3(BandStep(_BandThresholds.x, intensity),
                         BandStep(_BandThresholds.y, intensity),
                         BandStep(_BandThresholds.z, intensity));
    half level = lerp(_BandLevels.x, _BandLevels.y, weights.x);
    level = lerp(level, _BandLevels.z, weights.y);
    level = lerp(level, _BandLevels.w, weights.z);
    half3 tint = lerp(_ShadowTint.rgb, _MidTint.rgb, weights.x);
    tint = lerp(tint, _LightTint.rgb, weights.y);
    tint = lerp(tint, _HighlightTint.rgb, weights.z);
    half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
    half3 ambient = max(SampleSH(inputData.normalWS), 0) * _AmbientStrength;
    ambient *= lerp(_ShadowTint.rgb, half3(1, 1, 1), weights.x) * ao.indirectAmbientOcclusion;
    half3 diffuseColor = albedo * (1 - 0.65h * _Metallic);
    half3 result = diffuseColor * (ambient + hue * level * tint * ao.directAmbientOcclusion);
    half3 specularColor = lerp(half3(0.04, 0.04, 0.04), albedo, _Metallic);
    result += min(specular, 2.0) * specularColor * _SpecularStrength * ao.directAmbientOcclusion;
#if defined(_EMISSION)
    result += _EmissionColor.rgb;
#endif
    return half4(MixFog(result, input.fogFactor), 1);
}
#endif
