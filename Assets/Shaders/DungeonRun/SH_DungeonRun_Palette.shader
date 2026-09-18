Shader "DungeonRun/SH_DungeonRun_Palette"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "LuminancePreservingPalette"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #define USE_FULL_PRECISION_BLIT_TEXTURE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X_FLOAT(_PaletteDepth);
            float4 _VirtualResolution, _Treatment, _DepthAccent;
            float4 _ColdStone, _Amber, _Jade, _Terracotta, _Bone, _Cyan;
            float _AccentProtection;
            static const float3 LUMA = float3(.2126, .7152, .0722);

            float3 Chroma(float3 rgb) { return rgb - dot(rgb, LUMA); }
            float3 Direction(float3 c) { return c / max(length(c), 1e-7); }
            void Accumulate(float3 rgb, float3 sourceDirection, inout float3 sum, inout float weightSum)
            {
                float3 direction = Direction(Chroma(rgb));
                // Continuous spherical weighting, no hue sectors, nearest-color lookup or posterization.
                float weight = exp(_Treatment.w * (dot(sourceDirection, direction) - 1));
                sum += direction * weight;
                weightSum += weight;
            }
            float GamutLimit(float component, float y, float ceiling)
            {
                // One scalar contracts chroma toward gray, preserving Y; never clamp RGB channels independently.
                return component < 0 ? y / max(-component, 1e-7) :
                    (component > 0 ? (ceiling - y) / max(component, 1e-7) : 1);
            }
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 cell = min(floor(saturate(input.texcoord) * _VirtualResolution.xy), _VirtualResolution.xy - 1);
                float2 uv = (cell + .5) * _VirtualResolution.zw;
                // Color AND depth sample the identical virtual-cell center: every final 4x4 block stays uniform.
                float4 source = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_PaletteDepth, sampler_PointClamp, uv, 0).r;
                float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float y = dot(source.rgb, LUMA);
                float3 originalChroma = Chroma(source.rgb);
                float magnitude = length(originalChroma);
                float3 direction = Direction(originalChroma);
                float3 sum = 0;
                float weights = 0;
                Accumulate(_ColdStone.rgb, direction, sum, weights);
                Accumulate(_Amber.rgb, direction, sum, weights);
                Accumulate(_Jade.rgb, direction, sum, weights);
                Accumulate(_Terracotta.rgb, direction, sum, weights);
                Accumulate(_Bone.rgb, direction, sum, weights);
                Accumulate(_Cyan.rgb, direction, sum, weights);
                float3 groupedChroma = Direction(sum / max(weights, 1e-7)) * magnitude;
                // Neutral/near-black source colors remain neutral; family colors transfer direction, never value.
                float chromatic = smoothstep(.05, .35, magnitude / max(y, .002));
                float accent = smoothstep(_DepthAccent.z, _DepthAccent.w, y) * _AccentProtection;
                float grouping = _Treatment.x * chromatic * (1 - accent);
                float background = smoothstep(_DepthAccent.x, _DepthAccent.y, eyeDepth);
                float retention = lerp(_Treatment.y, _Treatment.z, background);
                retention = lerp(retention, 1, accent);
                float3 c = Chroma(lerp(originalChroma, groupedChroma, grouping)) * retention;
                // Algebra: dot(c,LUMA)=0, so dot(y+c,LUMA)=y. Gamut scaling retains that invariant.
                // Keep HDR headroom at least as large as the source peak, instead of crushing values to one.
                float ceiling = max(1, max(source.r, max(source.g, source.b)));
                float scale = saturate(min(GamutLimit(c.r, y, ceiling),
                    min(GamutLimit(c.g, y, ceiling), GamutLimit(c.b, y, ceiling))));
                return float4(y + c * scale, source.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
