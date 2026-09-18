Shader "DungeonRun/SH_DungeonRun_SelectiveOutlineComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "VirtualGridInnerContour"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #define USE_FULL_PRECISION_BLIT_TEXTURE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_OutlineMask);
            float4 _VirtualResolution, _OutlineColor;
            float Identity(float2 uv)
            { return SAMPLE_TEXTURE2D_X_LOD(_OutlineMask, sampler_PointClamp, uv, 0).r; }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 cell = min(floor(saturate(input.texcoord) * _VirtualResolution.xy), _VirtualResolution.xy - 1);
                float2 uv = (cell + .5) * _VirtualResolution.zw;
                float2 center = SAMPLE_TEXTURE2D_X_LOD(_OutlineMask, sampler_PointClamp, uv, 0).rg;
                float edge = max(max(abs(center.r - Identity(uv + float2(_VirtualResolution.z, 0))),
                    abs(center.r - Identity(uv - float2(_VirtualResolution.z, 0)))),
                    max(abs(center.r - Identity(uv + float2(0, _VirtualResolution.w))),
                    abs(center.r - Identity(uv - float2(0, _VirtualResolution.w)))));
                float4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0);
                if (center.r > .5 && center.g > .5 && edge > .5)
                    source.rgb = _OutlineColor.rgb;
                return source;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
