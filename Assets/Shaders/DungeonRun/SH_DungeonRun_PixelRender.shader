Shader "DungeonRun/SH_DungeonRun_PixelRender"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "SpatialPixelGrid"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #define USE_FULL_PRECISION_BLIT_TEXTURE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _VirtualResolution;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // URP's fullscreen vertex supplies the render-target-oriented UVs.
                // Every cell reads exactly its center. No RGB quantization, blending or time input.
                float2 cell = min(floor(saturate(input.texcoord) * _VirtualResolution.xy),
                    _VirtualResolution.xy - 1.0);
                float2 uv = (cell + 0.5) * _VirtualResolution.zw;
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
