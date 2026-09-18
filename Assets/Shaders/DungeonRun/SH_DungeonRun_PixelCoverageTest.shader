// Isolated V1.1 experiment. Never assigned to a persistent material by the capture harness.
Shader "DungeonRun/SH_DungeonRun_PixelCoverageTest"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "SpatialPixelCoverage4Test"
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
                float2 cell = min(floor(saturate(input.texcoord) * _VirtualResolution.xy),
                    _VirtualResolution.xy - 1.0);
                float2 uv = (cell + 0.5) * _VirtualResolution.zw;
                float2 offset = 0.25 * _VirtualResolution.zw;
                // Four point samples cover the cell; the final cell remains spatially constant.
                float4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + offset, 0);
                color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv - offset, 0);
                color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + float2(offset.x, -offset.y), 0);
                color += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + float2(-offset.x, offset.y), 0);
                return color * 0.25;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
