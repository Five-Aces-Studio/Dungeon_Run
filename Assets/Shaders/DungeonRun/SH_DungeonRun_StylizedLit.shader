Shader "DungeonRun/SH_DungeonRun_StylizedLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,1)
        _ShadowTint("Shadow Tint", Color) = (0.72,0.8,0.94,1)
        _MidTint("Midtone Tint", Color) = (0.94,0.97,1,1)
        _LightTint("Light Tint", Color) = (1,1,1,1)
        _HighlightTint("Highlight Tint", Color) = (1,0.97,0.9,1)
        _BandThresholds("Band Thresholds (XYZ)", Vector) = (0.12,0.4,0.8,0)
        _BandLevels("Four Band Levels", Vector) = (0.06,0.28,0.62,1.1)
        _BandSoftness("Band Transition Softness", Range(0,0.15)) = 0.045
        _AmbientStrength("Ambient Strength", Range(0,2)) = 1.15
        _AdditionalLightStrength("Additional Light Strength", Range(0,2)) = 1
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Highlight Tightness", Range(0,1)) = 0.18
        _SpecularStrength("Stylized Specular Strength", Range(0,2)) = 0.05
        _SpecularThreshold("Specular Threshold", Range(0.1,0.95)) = 0.5

        [Header(Acrylic V3)]
        [Toggle(_DR_ACRYLIC)] _DRAcrylic("Acrylic Painterly Response", Float) = 0
        [NoScaleOffset] _BrushMap("Paint Stroke Map (linear RGBA: value, hue id, bristle, blotch)", 2D) = "grey" {}
        [Enum(World Triplanar,0,Object Triplanar,1,UV0,2)] _BrushMapping("Brush Mapping", Float) = 0
        _BrushStrength("Brush Strength", Range(0,1)) = 0
        _BrushScale("Brush Scale (tiles per metre or UV unit)", Range(0.05,8)) = 0.5
        _LightWrap("Light Wrap (soft terminator)", Range(0,1)) = 0
        _ShadowBreakup("Shadow Edge Breakup", Range(0,1)) = 0
        _RimStrength("Rim Strength", Range(0,1)) = 0
        _TouchColorA("Colour Touch A", Color) = (0.55,0.47,0.26,1)
        _TouchColorB("Colour Touch B", Color) = (0.26,0.30,0.42,1)
        _TouchAmount("Colour Touch (A fraction, B fraction, strength)", Vector) = (0,0,0,0)
        [HideInInspector] _PaintVersion("Paint Version", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On
        HLSLINCLUDE
        #include "DungeonRunStylizedInput.hlsl"
        ENDHLSL

        Pass
        {
            Name "StylizedForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex StylizedVertex
            #pragma fragment StylizedFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _DR_ACRYLIC
            #include "DungeonRunStylizedForward.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
