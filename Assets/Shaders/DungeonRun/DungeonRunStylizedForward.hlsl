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

#if defined(_DR_ACRYLIC)
// Acrylic V3 globals. Fragment-only; zero-neutral (unset acrylic materials degrade to the banded look).
float4 _DR_Painterly;
float4 _DR_Brush;
float4 _DR_CoolColor;
float4 _DR_Light; // x = light headroom (slope above the painted value range)
// Painted glow pools, decoupled from the physical lights. Zero count/strength => zero contribution.
float4 _DR_PoolPos[8];    // xyz world position, w radius (m)
float4 _DR_PoolColor[8];  // rgb linear colour * intensity * current flicker, w unused
float4 _DR_PoolParams;    // x count (0..8), y global strength, z edge breakup, w painted steps (0 = smooth)

// Paint stroke map channels (linear RGBA): R dab value, G dab hue id, B bristle streaks, A soft blotch.
struct BrushSample
{
    float dabValue;  // octave 1 R, centred [-1,1]: flat value per brush dab (large strokes)
    float dabHue;    // octave 1 G, [0,1]: discrete per-dab hue id (selects the colour-touch dabs)
    float blotch;    // octave 1 A, centred: low-frequency value drift, independent of the dabs
    float fineValue; // octave 2 R, centred: small strokes
    float bristle;   // octave 2 B, centred: bristle streaks along the small strokes
    float zone;      // procedural, centred [-1,1]: very low-frequency colour zones (where the touches gather)
};

// Cheap sin-free hash (Hoskins' hash13): [0,1) per integer lattice point.
float DRHash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// 3D value noise in [0,1]: hashed lattice, smoothstep-interpolated.
float DRValueNoise3(float3 p)
{
    float3 cell = floor(p);
    float3 f = p - cell;
    float3 u = f * f * (3 - 2 * f);
    float n000 = DRHash13(cell);
    float n100 = DRHash13(cell + float3(1, 0, 0));
    float n010 = DRHash13(cell + float3(0, 1, 0));
    float n110 = DRHash13(cell + float3(1, 1, 0));
    float n001 = DRHash13(cell + float3(0, 0, 1));
    float n101 = DRHash13(cell + float3(1, 0, 1));
    float n011 = DRHash13(cell + float3(0, 1, 1));
    float n111 = DRHash13(cell + float3(1, 1, 1));
    float n00 = lerp(n000, n100, u.x);
    float n10 = lerp(n010, n110, u.x);
    float n01 = lerp(n001, n101, u.x);
    float n11 = lerp(n011, n111, u.x);
    return lerp(lerp(n00, n10, u.y), lerp(n01, n11, u.y), u.z);
}

float3 DRTriplanarWeights(float3 n)
{
    float3 w = pow(abs(n), 4);
    return w / max(dot(w, 1), 1e-5);
}

// ~37 degrees; an incommensurate rotation so the detail set never lines up with the base set.
float2 DRRotate37(float2 p)
{
    const float c = 0.7986355, s = 0.6018150;
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

// Branchless: every mode candidate is always computed and blended by a one-hot selection vector, so the
// compiler never has to predicate a texture sample on _BrushMapping. Always exactly 6 taps.
BrushSample SampleBrush(float3 positionWS, float3 positionOS, float3 normalWS, float3 normalOS, float2 uv,
    float mapping, float scale)
{
    float selWorld = saturate(1 - saturate(mapping));
    float selObject = saturate(1 - abs(mapping - 1));
    float selUV0 = saturate(mapping - 1);

    float3 worldWeights = DRTriplanarWeights(normalWS);
    float3 objectWeights = DRTriplanarWeights(normalOS);
    const float3 uv0Weights = float3(1, 0, 0);
    float3 weights = selWorld * worldWeights + selObject * objectWeights + selUV0 * uv0Weights;

    float2 worldUV0 = positionWS.yz * scale;
    // Up-facing projection counter-foreshortened for the low grazing combat camera: rotated ~25 degrees so the dabs
    // do not all run parallel to the screen, and stretched 2.2x along depth so they read chunky instead of as thin
    // horizontal lines.
    float2 floorP = float2(positionWS.x * 0.906 - positionWS.z * 0.423, positionWS.x * 0.423 + positionWS.z * 0.906);
    float2 worldUV1 = floorP * scale * float2(1, 0.45);
    float2 worldUV2 = positionWS.xy * scale;
    float2 objectUV0 = positionOS.yz * scale;
    float2 objectUV1 = positionOS.xz * scale;
    float2 objectUV2 = positionOS.xy * scale;
    float2 uvUV = uv * scale;

    float2 baseUV0 = selWorld * worldUV0 + selObject * objectUV0 + selUV0 * uvUV;
    float2 baseUV1 = selWorld * worldUV1 + selObject * objectUV1 + selUV0 * uvUV;
    float2 baseUV2 = selWorld * worldUV2 + selObject * objectUV2 + selUV0 * uvUV;

    // Octave 2 ("detail"): smaller strokes over the large ones. Incommensurate scale and a ~37 degree rotation so
    // the two stroke sets never line up or tile in phase.
    const float detailScale = 2.31;
    const float2 detailOffset = float2(0.37, 0.61);
    float2 detailUV0 = DRRotate37(baseUV0 * detailScale) + detailOffset;
    float2 detailUV1 = DRRotate37(baseUV1 * detailScale) + detailOffset;
    float2 detailUV2 = DRRotate37(baseUV2 * detailScale) + detailOffset;

    // Octave 1 ("dab") at the base scale.
    float4 base0 = SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, baseUV0);
    float4 base1 = SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, baseUV1);
    float4 base2 = SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, baseUV2);
    float4 detail0 = SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, detailUV0);
    float4 detail1 = SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, detailUV1);
    float4 detail2 = SAMPLE_TEXTURE2D(_BrushMap, sampler_BrushMap, detailUV2);

    BrushSample result;
    result.dabValue = weights.x * base0.r + weights.y * base1.r + weights.z * base2.r;
    result.dabHue = weights.x * base0.g + weights.y * base1.g + weights.z * base2.g;
    result.blotch = weights.x * base0.a + weights.y * base1.a + weights.z * base2.a;
    result.fineValue = weights.x * detail0.r + weights.y * detail1.r + weights.z * detail2.r;
    result.bristle = weights.x * detail0.b + weights.y * detail1.b + weights.z * detail2.b;

    // Center to [-1,1]; the baked channels are normalized to use most of [0,1] with mean near 0.5. The hue id
    // stays in [0,1]: it is a selector, not a signed modulation.
    result.dabValue = result.dabValue * 2 - 1;
    result.blotch = result.blotch * 2 - 1;
    result.fineValue = result.fineValue * 2 - 1;
    result.bristle = result.bristle * 2 - 1;

    // Colour zones: procedural 3D value noise of the mapping position (world for World Triplanar, object for Object
    // Triplanar and UV0; same one-hot selection as the taps) at a fixed frequency independent of the dab scale:
    // 0.25 cycles per metre in the world (a few zones per wall/floor), 0.9 per object unit on characters. The fixed
    // rotation keeps the lattice from lining up with axis-aligned walls and floors.
    const float3x3 zoneRotation = float3x3(0.00, 0.80, 0.60,
                                           -0.80, 0.36, -0.48,
                                           -0.60, -0.48, 0.64);
    float3 zonePosition = selWorld * positionWS + (1 - selWorld) * positionOS;
    float zoneFrequency = selWorld * 0.25 + (1 - selWorld) * 0.9;
    result.zone = DRValueNoise3(mul(zoneRotation, zonePosition * zoneFrequency)) * 2 - 1;
    return result;
}

// A colour touch keeps its own hue/chroma but mostly follows the surrounding paint's value (18% of its own), so it
// reads as a temperature shift worked into the shaded mass instead of a value patch (camouflage).
half3 DRLumaMatchedTouch(half3 touch, half albedoLuma)
{
    half touchLuma = max(dot(touch, half3(0.2126, 0.7152, 0.0722)), 1e-4);
    return touch * (lerp(albedoLuma, touchLuma, 0.18) / touchLuma);
}

// Lit-space half of a colour touch: pulls the lit colour toward the touch hue at the lit colour's own luminance (a
// pure hue/chroma change, no value change, valid for HDR), so the touch survives strongly coloured light instead of
// being multiplied into the light's hue. Zero weight or a black touch colour leave the colour unchanged.
half3 DRTouchHuePush(half3 color, half3 touch, half weight)
{
    half touchLuma = dot(touch, half3(0.2126, 0.7152, 0.0722));
    half colorLuma = dot(color, half3(0.2126, 0.7152, 0.0722));
    half3 matched = touch * (colorLuma / max(touchLuma, 1e-4));
    return lerp(color, matched, touchLuma > 1e-4 ? weight : 0);
}

// Painted glow pools: soft value pools around the fixtures, decoupled from the physical light falloff. The dab
// value breaks the pool edge into stroke shapes; optional painted steps give flat, softly joined value rings.
float3 DRPaintedPools(float3 positionWS, float3 normalWS, float dabValue)
{
    float3 pool = 0;
    int count = min((int)_DR_PoolParams.x, 8);
    float breakup = _DR_PoolParams.z;
    float steps = _DR_PoolParams.w;
    [loop] for (int poolIndex = 0; poolIndex < count; ++poolIndex)
    {
        float4 poolPos = _DR_PoolPos[poolIndex];
        float3 toPool = poolPos.xyz - positionWS;
        float dist = length(toPool);
        float t = saturate(1 - dist / max(poolPos.w, 1e-3));
        t = saturate(t * (1 + dabValue * breakup));
        if (steps > 0)
        {
            float s = t * steps;
            t = (floor(s) + smoothstep(0.3, 0.7, frac(s))) / steps;
        }
        float facing = saturate(dot(normalWS, toPool / max(dist, 1e-4)) * 0.6 + 0.4);
        pool += _DR_PoolColor[poolIndex].rgb * t * t * facing;
    }
    return pool;
}
#endif

void AccumulateLight(Light light, half3 normalWS, half3 viewWS, bool isAdditionalLight,
                     inout float3 diffuse, inout float3 specular)
{
#if defined(_DR_ACRYLIC)
    float wrap = _LightWrap * _DR_Painterly.x;
    half ndotl = saturate((dot(normalWS, light.direction) + wrap) / (1 + wrap));
    float attenuation = light.distanceAttenuation;
    if (isAdditionalLight)
    {
        // Broadens torch pools: dims the hot spot, extends the falloff, unchanged at the 0.25 (2 m) reference.
        attenuation = 0.25 * pow(max(attenuation, 1e-6) / 0.25, lerp(1.0, 0.5, _DR_Painterly.z));
    }
    float3 radiance = light.color * attenuation * light.shadowAttenuation;
#else
    half ndotl = saturate(dot(normalWS, light.direction));
    float3 radiance = light.color * light.distanceAttenuation * light.shadowAttenuation;
#endif
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

#if defined(_DR_ACRYLIC)
    // Object-space coordinates are fragment-only (per-instance via UNITY_MATRIX_I_M); the vertex stage and
    // Varyings are unchanged so the keyword-off path is byte-identical.
    float3 positionOS = TransformWorldToObject(input.positionWS);
    // Normals need the inverse-transpose transform (not the plain direction transform used for positions/vectors)
    // to stay correct under non-uniform object scale.
    float3 normalOS = TransformWorldToObjectNormal(inputData.normalWS);
    BrushSample brush = SampleBrush(input.positionWS, positionOS, inputData.normalWS, normalOS, input.uv,
        _BrushMapping, _BrushScale);
    float paint = _BrushStrength * _DR_Painterly.x;

    // World-stable tangent-plane basis of the surface normal (NOT the light direction: that would not move
    // the lookup in shadow-map space).
    float3 upHint = (abs(inputData.normalWS.y) > 0.99) ? float3(1, 0, 0) : float3(0, 1, 0);
    float3 tangent1 = normalize(cross(inputData.normalWS, upHint));
    float3 tangent2 = cross(inputData.normalWS, tangent1);
    float3 shadowOffset = (brush.dabValue * tangent1 + brush.bristle * tangent2) * _ShadowBreakup * _DR_Brush.w;
    float3 shadowSamplePositionWS = input.positionWS + shadowOffset;
#else
    float3 shadowSamplePositionWS = input.positionWS;
#endif

#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    // Screen-space shadows are a per-screen-pixel value pre-resolved by a separate full-screen pass against the
    // real scene depth (see MainLightRealtimeShadow/GetShadowCoord in URP's Shadows.hlsl). Perturbing positionWS
    // before this transform would not move the lookup in shadow-map space; it would sample a different screen
    // pixel's already-resolved value, so this coordinate must stay the fragment's own unperturbed screen UV.
    inputData.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
    inputData.shadowCoord = TransformWorldToShadowCoord(shadowSamplePositionWS);
#endif
    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    half4 shadowMask = half4(1, 1, 1, 1);
    float3 direct = 0;
    float3 specular = 0;
    Light mainLight = GetMainLight(inputData.shadowCoord, input.positionWS, shadowMask);
    AccumulateLight(mainLight, inputData.normalWS, inputData.viewDirectionWS, false, direct, specular);

#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
    float3 additionalDiffuse = 0;
    float3 additionalSpecular = 0;
    // URP 17.3: clustered directionals are separate from the punctual cluster iterator.
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
        AccumulateLight(light, inputData.normalWS, inputData.viewDirectionWS, true, additionalDiffuse, additionalSpecular);
    }
#endif
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
        AccumulateLight(light, inputData.normalWS, inputData.viewDirectionWS, true, additionalDiffuse, additionalSpecular);
    LIGHT_LOOP_END
    direct += additionalDiffuse * _AdditionalLightStrength;
    specular += additionalSpecular * _AdditionalLightStrength;
#endif

    // Quantize combined irradiance once; retain relative warm/cool light color.
    // bandIntensity (terminator-breakup-modulated) drives banding only; hue's own denominator stays the
    // unmodified per-channel max so hue remains a pure unit-magnitude colour direction, not double-modulated
    // by the same exp2() brightness term that level/weights already apply.
    float intensity = max(direct.r, max(direct.g, direct.b));
    float bandIntensity = intensity;
#if defined(_DR_ACRYLIC)
    // Value boundaries follow the dabs, so the terminator reads as stroke-shaped paint edges.
    bandIntensity *= exp2((brush.dabValue * 0.7 + brush.bristle * 0.3) * paint * _DR_Brush.z);
#endif
    // In complete shadow there is no direct-light hue to normalize. Blend to
    // neutral at zero so the lowest band retains its authored shadow tint;
    // above the first threshold the actual combined light hue is unchanged.
    half chromaticWeight = saturate(intensity / max(_BandThresholds.x, 0.0001));
    half3 hue = lerp(half3(1, 1, 1), direct / max(intensity, 0.0001), chromaticWeight);
#if defined(_DR_ACRYLIC)
    float bandT0 = _BandThresholds.x, bandT1 = _BandThresholds.y, bandT2 = _BandThresholds.z;
    float softMax0 = max(bandT0, 1e-4);
    float softMax1 = max(bandT1 - bandT0, 1e-4);
    float softMax2 = max(bandT2 - bandT1, 1e-4);
    float soft0 = lerp(_BandSoftness, softMax0, _DR_Painterly.y);
    float soft1 = lerp(_BandSoftness, softMax1, _DR_Painterly.y);
    float soft2 = lerp(_BandSoftness, softMax2, _DR_Painterly.y);
    half3 weights = half3(
        smoothstep(bandT0 - soft0, bandT0 + soft0, bandIntensity),
        smoothstep(bandT1 - soft1, bandT1 + soft1, bandIntensity),
        smoothstep(bandT2 - soft2, bandT2 + soft2, bandIntensity));
#else
    half3 weights = half3(BandStep(_BandThresholds.x, intensity),
                         BandStep(_BandThresholds.y, intensity),
                         BandStep(_BandThresholds.z, intensity));
#endif
    half level = lerp(_BandLevels.x, _BandLevels.y, weights.x);
    level = lerp(level, _BandLevels.z, weights.y);
    level = lerp(level, _BandLevels.w, weights.z);
#if defined(_DR_ACRYLIC)
    // Light headroom: strong lights (torches, combat pools) keep rising above the painted value range, so warm
    // pools read as bright gradients instead of being flattened to the top band.
    level += max(0, bandIntensity - (bandT2 + softMax2)) * _DR_Light.x * _DR_Painterly.y;
#endif
    half3 tint = lerp(_ShadowTint.rgb, _MidTint.rgb, weights.x);
    tint = lerp(tint, _LightTint.rgb, weights.y);
    tint = lerp(tint, _HighlightTint.rgb, weights.z);
    half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
#if defined(_DR_ACRYLIC)
    // Paint value: flat value per large dab, small strokes on top, slow blotch drift underneath.
    albedo *= 1.0 + (brush.dabValue * 0.6 + brush.fineValue * 0.25 + brush.blotch * 0.35) * paint * _DR_Brush.x;
    // Colour touches: a discrete fraction of the dabs (picked by the per-dab hue id) is repainted in touch hue A
    // or B, luminance-matched so it shifts hue, not value. The amount guards make zero amounts select no dab at
    // all (hue ids of exactly 0 or 1 would otherwise be half-selected by the smoothstep).
    float touchScale = saturate(paint * _DR_Brush.y * 1.5);
    // Touches follow the light, as a painter distributes temperature: the cooler touch colour goes into the shaded
    // half-tones, the warmer one into the lit half-tones; deep shadows and the brightest focal light stay clean
    // masses. The low-frequency zone noise only modulates density (0.5-1x) so coverage is never uniform. The two
    // light windows barely overlap, so A and B rarely compete for the same dab.
    half lightRaw = bandIntensity / max(_BandThresholds.z, 1e-3);
    half light01 = saturate(lightRaw);
    half shadeTone = smoothstep(0.04, 0.25, light01) * (1 - smoothstep(0.4, 0.65, light01));
    // The torch-lit focal floor keeps some warm touches; only the hottest highlights thin them out (to 30%).
    half litTone = smoothstep(0.35, 0.6, lightRaw) * (1 - 0.7 * smoothstep(1.0, 1.6, lightRaw));
    half aIsWarm = step(_TouchColorB.r - _TouchColorB.b, _TouchColorA.r - _TouchColorA.b);
    half zoneDensity = 0.5 + 0.5 * smoothstep(-0.4, 0.4, brush.zone);
    // The cool touch also reaches into the lit half-tones at reduced density, so a warm-lit surface still gets a
    // broken warm/cool pair (A and B pick disjoint dab ids, so they never compete for the same dab).
    half coolTone = shadeTone + 0.3 * litTone * (1 - shadeTone);
    float amountA = min(_TouchAmount.x * touchScale * 2 * zoneDensity * lerp(coolTone, litTone, aIsWarm), 0.6);
    float amountB = min(_TouchAmount.y * touchScale * 2 * zoneDensity * lerp(litTone, coolTone, aIsWarm), 0.6);
    half selA = amountA > 1e-4 ? 1 - smoothstep(amountA - 0.015, amountA + 0.015, brush.dabHue) : 0;
    half selB = amountB > 1e-4 ? smoothstep(1 - amountB - 0.015, 1 - amountB + 0.015, brush.dabHue) : 0;
    // Half of each touch is repainted into the albedo here; the other half is a lit-space hue push after lighting,
    // so warm torch light does not multiply every touch into the same ochre.
    // Each touch dab gets its own paint load (pseudo-random from its hue id) and a dry-brush broken edge from the
    // small strokes, so touches read as brushed strokes instead of flat, identical stickers.
    half dabLoad = lerp(0.55, 1.0, frac(brush.dabHue * 37.13 + 0.21));
    half dryBrush = saturate(0.55 + brush.fineValue * 0.9);
    half touchWeightA = selA * _TouchAmount.z * 0.5 * dabLoad * dryBrush;
    half touchWeightB = selB * _TouchAmount.z * 0.5 * dabLoad * dryBrush;
    half albedoLuma = dot(albedo, half3(0.2126, 0.7152, 0.0722));
    half3 touchA = DRLumaMatchedTouch(_TouchColorA.rgb, albedoLuma);
    half3 touchB = DRLumaMatchedTouch(_TouchColorB.rgb, albedoLuma);
    albedo = lerp(albedo, touchA, touchWeightA);
    albedo = lerp(albedo, touchB, touchWeightB);
    // Warm/cool bristle tint variation, at half the previous weight: the touches now carry the hue variety.
    half colorT = brush.bristle * 0.5 + 0.5;
    half3 colorTint = lerp(_ShadowTint.rgb, _HighlightTint.rgb, colorT);
    colorTint /= max(dot(colorTint, half3(0.2126, 0.7152, 0.0722)), 1e-4);
    albedo *= lerp(half3(1, 1, 1), colorTint, paint * _DR_Brush.y * 0.5);
#endif
    half3 ambient = max(SampleSH(inputData.normalWS), 0) * _AmbientStrength;
    ambient *= lerp(_ShadowTint.rgb, half3(1, 1, 1), weights.x) * ao.indirectAmbientOcclusion;
#if defined(_DR_ACRYLIC)
    // Ambient coolness: pull toward the profile's cool shadow color, preserving luminance.
    half ambientLuma = dot(ambient, half3(0.2126, 0.7152, 0.0722));
    half coolLuma = max(dot(_DR_CoolColor.rgb, half3(0.2126, 0.7152, 0.0722)), 1e-4);
    half3 ambientCool = ambientLuma * (_DR_CoolColor.rgb / coolLuma);
    ambient = lerp(ambient, ambientCool, _DR_Painterly.w);
#endif
    half3 diffuseColor = albedo * (1 - 0.65h * _Metallic);
    half3 result = diffuseColor * (ambient + hue * level * tint * ao.directAmbientOcclusion);
    half3 specularColor = lerp(half3(0.04, 0.04, 0.04), albedo, _Metallic);
    result += min(specular, 2.0) * specularColor * _SpecularStrength * ao.directAmbientOcclusion;
#if defined(_DR_ACRYLIC)
    // Painted glow pools lift the albedo like extra light, after the main result and before rim/emission.
    result += diffuseColor * DRPaintedPools(input.positionWS, inputData.normalWS, brush.dabValue) * _DR_PoolParams.y;
    // Lit-space half of the colour touches (after all lighting, before rim/fog/emission).
    result = DRTouchHuePush(result, _TouchColorA.rgb, touchWeightA);
    result = DRTouchHuePush(result, _TouchColorB.rgb, touchWeightB);
    // Rim is not scaled by influence: it is a per-material author control, always live once _RimStrength > 0.
    half fresnel = pow(1 - saturate(dot(inputData.normalWS, inputData.viewDirectionWS)), 3);
    half3 rimAmbient = max(SampleSH(inputData.normalWS), 0);
    result += _RimStrength * fresnel * (rimAmbient + 0.25 * mainLight.color * mainLight.shadowAttenuation) *
        _HighlightTint.rgb * 0.5;
#endif
#if defined(_EMISSION)
    result += _EmissionColor.rgb;
#endif
#if defined(_DR_ACRYLIC) && defined(_EMISSION)
    // Painted glow: emissive glass varies dab to dab like brushed paint instead of a flat emissive block.
    result += _EmissionColor.rgb * brush.dabValue * 0.35 * _DR_Painterly.x;
#endif
    return half4(MixFog(result, input.fogFactor), 1);
}
#endif
