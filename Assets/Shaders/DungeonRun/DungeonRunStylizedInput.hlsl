#ifndef DUNGEON_RUN_STYLIZED_INPUT_INCLUDED
#define DUNGEON_RUN_STYLIZED_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Identical layout in every pass for SRP Batcher compatibility.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _EmissionColor;
    half4 _ShadowTint;
    half4 _MidTint;
    half4 _LightTint;
    half4 _HighlightTint;
    float4 _BandThresholds;
    float4 _BandLevels;
    float _BandSoftness;
    float _AmbientStrength;
    float _AdditionalLightStrength;
    float _Metallic;
    float _Smoothness;
    float _SpecularStrength;
    float _SpecularThreshold;
    float _BrushMapping;
    float _BrushStrength;
    float _BrushScale;
    float _LightWrap;
    float _ShadowBreakup;
    float _RimStrength;
    half4 _TouchColorA;
    half4 _TouchColorB;
    float4 _TouchAmount; // x = fraction A, y = fraction B, z = strength, w unused
CBUFFER_END
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_BrushMap);
SAMPLER(sampler_BrushMap);
#endif
