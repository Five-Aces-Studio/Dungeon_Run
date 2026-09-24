Shader "DungeonRun/SH_DungeonRun_WorldStyle"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #define USE_FULL_PRECISION_BLIT_TEXTURE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        TEXTURE2D_X_FLOAT(_WorldStyleDepth);

        // Paint filter (passes 1-5). Half-res buffers hold rgb = sqrt(LDR colour) (gamma 2, so the filter statistics
        // roughly follow perceived value) and a = linear eye depth in metres.
        TEXTURE2D_X(_WS_PaintSrc);
        TEXTURE2D_X(_WS_TensorSrc);
        TEXTURE2D_X(_WS_Tensor);
        TEXTURE2D_X(_WS_Painted);
        // HalfTexel: 1/halfW, 1/halfH, halfW, halfH.
        float4 _WS_HalfTexel;
        ENDHLSL

        Pass
        {
            Name "WorldStyle"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            // Grid: cellPx, 1/cellPx, screenW, screenH.
            float4 _WS_Grid;
            // Pixel: pixelInfluence, edgeStrength, cellFilter, edgeDepthThresholdMetres.
            float4 _WS_Pixel;
            // Color: paletteStrength, saturation, contrast, shadowLift.
            float4 _WS_Color;
            // Quant: quantStrength, levels, ditherStrength, glowProtection.
            float4 _WS_Quant;
            // Atmos: desaturation, haze, fogStart, fogEnd.
            float4 _WS_Atmos;
            float4 _WS_FogColor;
            float4 _WS_CoolColor;
            // Focus (painter's vignette): strength, centreY (uv), radiusX (uv), radiusY (uv). FocusParams: softness.
            float4 _WS_Focus;
            float4 _WS_FocusParams;
            // Rim: strength, width (px at 1080p), relative depth-gap threshold, unused. RimDir: normalized screen
            // direction (+y up). RimDistance: full-strength eye depth (m), zero-strength eye depth (m), unused, unused.
            float4 _WS_Rim;
            float4 _WS_RimDir;
            float4 _WS_RimColor; // linear rgb
            float4 _WS_RimDistance;

            // Palette uniforms, named exactly as in SH_DungeonRun_Palette.shader.
            float4 _Treatment, _DepthAccent;
            float4 _ColdStone, _Amber, _Jade, _Terracotta, _Bone, _Cyan;
            float _AccentProtection;

            static const float3 LUMA = float3(.2126, .7152, .0722);
            static const float BAYER_4X4[16] = {
                0.0 / 16.0, 8.0 / 16.0, 2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0, 6.0 / 16.0,
                3.0 / 16.0, 11.0 / 16.0, 1.0 / 16.0, 9.0 / 16.0,
                15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0, 5.0 / 16.0
            };

            float BayerValue(uint2 p)
            {
                uint index = (p.y & 3u) * 4u + (p.x & 3u);
                return BAYER_4X4[index];
            }

            // --- Copied verbatim (same math) from SH_DungeonRun_Palette.shader; d substitutes for eye depth. ---
            float3 Chroma(float3 rgb) { return rgb - dot(rgb, LUMA); }
            float3 Direction(float3 c) { return c / max(length(c), 1e-7); }
            void Accumulate(float3 rgb, float3 sourceDirection, inout float3 sum, inout float weightSum)
            {
                float3 direction = Direction(Chroma(rgb));
                float weight = exp(_Treatment.w * (dot(sourceDirection, direction) - 1));
                sum += direction * weight;
                weightSum += weight;
            }
            float GamutLimit(float component, float y, float ceiling)
            {
                return component < 0 ? y / max(-component, 1e-7) :
                    (component > 0 ? (ceiling - y) / max(component, 1e-7) : 1);
            }
            float3 GroupPalette(float3 baseColor, float eyeDepth)
            {
                float y = dot(baseColor, LUMA);
                float3 originalChroma = Chroma(baseColor);
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
                float chromatic = smoothstep(.05, .35, magnitude / max(y, .002));
                float accent = smoothstep(_DepthAccent.z, _DepthAccent.w, y) * _AccentProtection;
                float grouping = _Treatment.x * chromatic * (1 - accent);
                float background = smoothstep(_DepthAccent.x, _DepthAccent.y, eyeDepth);
                float retention = lerp(_Treatment.y, _Treatment.z, background);
                retention = lerp(retention, 1, accent);
                float3 c = Chroma(lerp(originalChroma, groupedChroma, grouping)) * retention;
                float ceiling = max(1, max(baseColor.r, max(baseColor.g, baseColor.b)));
                float scale = saturate(min(GamutLimit(c.r, y, ceiling),
                    min(GamutLimit(c.g, y, ceiling), GamutLimit(c.b, y, ceiling))));
                return y + c * scale;
            }
            // --- End copied palette math. ---

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 1. Grid in device pixels, anchored at screen origin, clamped.
                float cellPx = max(_WS_Grid.x, 1.0);
                float2 screen = _WS_Grid.zw;
                float2 pixel = saturate(input.texcoord) * screen;
                float2 cell = floor(pixel / cellPx);
                float2 centre = min((cell + 0.5) * cellPx, screen - 0.5);
                float2 uvP = (floor(pixel) + 0.5) / screen;
                float2 uvC = centre / screen;

                // 2. Cell colour: lerp(centre point, 4-tap bilinear area, cellFilter); full-res point sample.
                float4 full = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uvP, 0);
                float4 centreCol = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uvC, 0);
                float2 halfTexel = 0.25 * cellPx / screen;
                float4 area = 0;
                area += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvC + float2(-halfTexel.x, -halfTexel.y), 0);
                area += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvC + float2(halfTexel.x, -halfTexel.y), 0);
                area += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvC + float2(-halfTexel.x, halfTexel.y), 0);
                area += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvC + float2(halfTexel.x, halfTexel.y), 0);
                area *= 0.25;
                float4 cellCol = lerp(centreCol, area, saturate(_WS_Pixel.z));

                // 3. Silhouette edge from depth.
                float rawP = SAMPLE_TEXTURE2D_X_LOD(_WorldStyleDepth, sampler_PointClamp, uvP, 0).r;
                float rawC = SAMPLE_TEXTURE2D_X_LOD(_WorldStyleDepth, sampler_PointClamp, uvC, 0).r;
                float dp = LinearEyeDepth(rawP, _ZBufferParams);
                float dc = LinearEyeDepth(rawC, _ZBufferParams);
                float edge = saturate(abs(dp - dc) / max(_WS_Pixel.w, 1e-4));

                // 4. Glow protection blends back toward the full-res sample on bright pixels.
                float glow = smoothstep(0.8, 1.6, max(dot(full.rgb, LUMA), dot(cellCol.rgb, LUMA))) * saturate(_WS_Quant.w);
                float mixAmount = saturate(_WS_Pixel.x + edge * _WS_Pixel.y) * (1 - glow);
                float4 c = lerp(full, cellCol, mixAmount);
                float d = lerp(dp, dc, mixAmount);

                // 5. Split HDR: keep bloom-feeding excess untouched by grading/quantization.
                float3 baseColor = min(c.rgb, 1.0);
                float3 excess = c.rgb - baseColor;

                // 6. Palette grouping on the base (LDR) part only.
                float paletteStrength = saturate(_WS_Color.x);
                if (paletteStrength > 0)
                {
                    float3 grouped = GroupPalette(baseColor, d);
                    baseColor = lerp(baseColor, grouped, paletteStrength);
                }

                // 7. Atmosphere from RenderSettings fog (start/end/colour), no duplicate distance params.
                float fogStart = _WS_Atmos.z;
                float fogEnd = _WS_Atmos.w;
                float f = saturate((d - fogStart) / max(fogEnd - fogStart, 1e-3));
                float baseLuma = dot(baseColor, LUMA);
                baseColor = lerp(baseColor, baseLuma.xxx, f * saturate(_WS_Atmos.x));
                baseColor = lerp(baseColor, _WS_FogColor.rgb, f * saturate(_WS_Atmos.y));
                // Painted backdrop: pixels with no geometry (the far-plane void, e.g. the gap between the floor edge and
                // the wall bases) take the fog colour instead of reading as an unpainted black strip. Off with haze 0.
                float backdropDepth = 0.99 * LinearEyeDepth(UNITY_RAW_FAR_CLIP_VALUE, _ZBufferParams);
                baseColor = lerp(baseColor, _WS_FogColor.rgb, step(backdropDepth, dp) * step(1e-4, _WS_Atmos.y));

                // 7a. Value focus (painter's vignette): darken the value masses away from the lit focal area.
                float focusStrength = _WS_Focus.x;
                if (focusStrength > 0)
                {
                    float focusSoftness = _WS_FocusParams.x;
                    float2 focusOffset = (uvP - float2(0.5, _WS_Focus.y)) / max(_WS_Focus.zw, 1e-3);
                    float focusMask = smoothstep(1 - focusSoftness, 1 + focusSoftness, length(focusOffset));
                    baseColor *= 1 - focusStrength * focusMask;
                }

                // 7b. Silhouette rim: painted edge light on silhouettes whose neighbour toward the rim direction is
                // relatively much farther away (a figure in front of deeper geometry). The gap is relative to the
                // pixel's own depth, so the threshold scales with distance instead of favouring the far background.
                // The rim fades with the pixel's eye depth (full up to RimDistance.x, none past RimDistance.y), and
                // neighbours on the far plane (the void behind the level) are ignored, so background towers, ledges,
                // chains and the horizon never outline against the void. Fades with the atmosphere like the base.
                float rimStrength = _WS_Rim.x;
                if (rimStrength > 0)
                {
                    float rimThreshold = max(_WS_Rim.z, 1e-3);
                    float2 rimStep = _WS_RimDir.xy * (_WS_Rim.y * screen.y / 1080.0) / screen;
                    float rawNear = SAMPLE_TEXTURE2D_X_LOD(_WorldStyleDepth, sampler_PointClamp, uvP + rimStep, 0).r;
                    float rawFar = SAMPLE_TEXTURE2D_X_LOD(_WorldStyleDepth, sampler_PointClamp, uvP + 2 * rimStep, 0).r;
                    // Cleared depth is exactly the far clip value; the 1% tolerance also catches geometry on the far
                    // plane. Void neighbours collapse to the pixel's own depth (zero gap).
                    float voidDepth = 0.99 * LinearEyeDepth(UNITY_RAW_FAR_CLIP_VALUE, _ZBufferParams);
                    float depthNear = LinearEyeDepth(rawNear, _ZBufferParams);
                    float depthFar = LinearEyeDepth(rawFar, _ZBufferParams);
                    depthNear = depthNear < voidDepth ? depthNear : dp;
                    depthFar = depthFar < voidDepth ? depthFar : dp;
                    float relativeGap = (max(depthNear, depthFar) - dp) / max(dp, 1e-3);
                    float rim = saturate((relativeGap - rimThreshold) / rimThreshold);
                    float rimFade = saturate((_WS_RimDistance.y - dp) / max(_WS_RimDistance.y - _WS_RimDistance.x, 1e-3));
                    baseColor += _WS_RimColor.rgb * rimStrength * rim * rimFade * (1 - f);
                }

                // 8. Grade: cool shadow lift, then saturation around luminance.
                baseColor += _WS_CoolColor.rgb * _WS_Color.w * (1 - smoothstep(0.0, 0.18, dot(baseColor, LUMA)));
                float gradeLuma = dot(baseColor, LUMA);
                baseColor = max(gradeLuma + (baseColor - gradeLuma) * _WS_Color.y, 0.0);

                // 9. sRGB contrast + ordered-dither quantization, aligned to the cell grid.
                float3 s = LinearToSRGB(saturate(baseColor));
                s = saturate((s - 0.5) * _WS_Color.z + 0.5);
                float levels = max(_WS_Quant.y, 2.0);
                float ditherValue = (BayerValue(uint2(cell)) - 0.5) * saturate(_WS_Quant.z);
                float3 q = floor(s * (levels - 1.0) + 0.5 + ditherValue) / (levels - 1.0);
                s = lerp(s, q, saturate(_WS_Quant.x) * (1 - glow));
                baseColor = SRGBToLinear(s);

                // 10. Recombine with the preserved HDR excess.
                return float4(baseColor + excess, full.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PaintDownsample"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPaintDownsample
            #pragma target 3.5

            float4 FragPaintDownsample(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // The target is half-res: one bilinear tap at the half-res texel centre is the 2x2 full-res box.
                float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord, 0).rgb;
                float raw = SAMPLE_TEXTURE2D_X_LOD(_WorldStyleDepth, sampler_PointClamp, input.texcoord, 0).r;
                // saturate == min(c, 1) for valid colour; it also keeps stray negative HDR values from producing NaN.
                return float4(sqrt(saturate(c)), LinearEyeDepth(raw, _ZBufferParams));
            }
            ENDHLSL
        }

        Pass
        {
            Name "PaintTensor"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPaintTensor
            #pragma target 3.5

            float3 PaintColor(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_WS_PaintSrc, sampler_PointClamp, uv, 0).rgb;
            }

            // Structure tensor (E, F, G) = (dx.dx, dx.dy, dy.dy) from a per-channel Sobel on the half-res paint source.
            float4 FragPaintTensor(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = _WS_HalfTexel.xy;
                float3 lb = PaintColor(uv + float2(-texel.x, -texel.y));
                float3 cb = PaintColor(uv + float2(0, -texel.y));
                float3 rb = PaintColor(uv + float2(texel.x, -texel.y));
                float3 lc = PaintColor(uv + float2(-texel.x, 0));
                float3 rc = PaintColor(uv + float2(texel.x, 0));
                float3 lt = PaintColor(uv + float2(-texel.x, texel.y));
                float3 ct = PaintColor(uv + float2(0, texel.y));
                float3 rt = PaintColor(uv + float2(texel.x, texel.y));
                float3 dx = (rb + 2 * rc + rt - lb - 2 * lc - lt) * 0.25;
                float3 dy = (lt + 2 * ct + rt - lb - 2 * cb - rb) * 0.25;
                return float4(dot(dx, dx), dot(dx, dy), dot(dy, dy), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PaintTensorBlur"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPaintTensorBlur
            #pragma target 3.5

            // BlurDir: (1, 0) horizontal or (0, 1) vertical, in half-res texels. C# runs H then V.
            float4 _WS_BlurDir;

            // 9-tap separable Gaussian, sigma = 2 half-res texels.
            float4 FragPaintTensorBlur(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 stepUV = _WS_BlurDir.xy * _WS_HalfTexel.xy;
                float4 sum = 0;
                float weightSum = 0;
                [unroll] for (int tap = -4; tap <= 4; ++tap)
                {
                    float weight = exp(-(tap * tap) / 8.0);
                    sum += weight * SAMPLE_TEXTURE2D_X_LOD(_WS_TensorSrc, sampler_PointClamp, input.texcoord + stepUV * tap, 0);
                    weightSum += weight;
                }
                return sum / weightSum;
            }
            ENDHLSL
        }

        Pass
        {
            Name "PaintKuwahara"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPaintKuwahara
            #pragma target 3.5

            // Kuwahara: radius (half-res px), sharpness q, anisotropy alpha, unused.
            float4 _WS_Kuwahara;

            // Hard cap on the sampled bounding box: offsets -8..8 per axis (17x17).
            static const int PAINT_MAX_OFFSET = 8;

            // Anisotropic Kuwahara filter (Kyprianidis et al. 2009) with polynomial sector weights (Kyprianidis 2011),
            // N = 8 sectors. The ellipse follows the local flow of the smoothed structure tensor, so the dabs stretch
            // along edges like brush strokes.
            float4 FragPaintKuwahara(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = _WS_HalfTexel.xy;
                float radius = max(_WS_Kuwahara.x, 1.0);
                float q = max(_WS_Kuwahara.y, 1.0);
                float alpha = max(_WS_Kuwahara.z, 1e-3);

                // Eigen-decomposition of the tensor (E, F, G): t is the flow (minor eigenvector) direction.
                float3 tensor = SAMPLE_TEXTURE2D_X_LOD(_WS_Tensor, sampler_PointClamp, uv, 0).xyz;
                float E = tensor.x, F = tensor.y, G = tensor.z;
                float root = sqrt(max((E - G) * (E - G) + 4 * F * F, 0));
                float lambda1 = 0.5 * (E + G + root);
                float lambda2 = 0.5 * (E + G - root);
                float2 t = float2(lambda1 - E, -F);
                float tLength2 = dot(t, t);
                t = tLength2 > 1e-12 ? t * rsqrt(max(tLength2, 1e-12)) : float2(0, 1);
                float phi = atan2(t.y, t.x);
                float lambdaSum = lambda1 + lambda2;
                float anisotropy = lambdaSum > 1e-8 ? (lambda1 - lambda2) / lambdaSum : 0;

                float a = radius * clamp((alpha + anisotropy) / alpha, 0.1, 2.0);
                float b = radius * clamp(alpha / (alpha + anisotropy), 0.1, 2.0);
                float cosPhi = cos(phi);
                float sinPhi = sin(phi);
                // SR = scale(0.5 / a, 0.5 / b) * rotate(-phi): maps the ellipse onto the disc of radius 0.5.
                float2x2 SR = float2x2(0.5 / a * cosPhi, 0.5 / a * sinPhi,
                                       -0.5 / b * sinPhi, 0.5 / b * cosPhi);
                int maxX = min((int)sqrt(a * a * cosPhi * cosPhi + b * b * sinPhi * sinPhi), PAINT_MAX_OFFSET);
                int maxY = min((int)sqrt(a * a * sinPhi * sinPhi + b * b * cosPhi * cosPhi), PAINT_MAX_OFFSET);

                // Sector polynomials: zero crossing at pi/N (N = 8) on the unit circle.
                const float sinPiN = 0.38268343;
                const float cosPiN = 0.92387953;
                float zeta = 2.0 / radius;
                float eta = (zeta + cosPiN) / (sinPiN * sinPiN);

                float4 m[8];
                float3 s[8];
                [unroll] for (int init = 0; init < 8; ++init)
                {
                    m[init] = 0;
                    s[init] = 0;
                }

                [loop] for (int j = -maxY; j <= maxY; ++j)
                {
                    [loop] for (int i = -maxX; i <= maxX; ++i)
                    {
                        float2 v = mul(SR, float2(i, j));
                        if (dot(v, v) <= 0.25)
                        {
                            float3 c = SAMPLE_TEXTURE2D_X_LOD(_WS_PaintSrc, sampler_PointClamp, uv + float2(i, j) * texel, 0).rgb;
                            float3 cc = c * c;

                            float w[8];
                            float z;
                            float vxx = zeta - eta * v.x * v.x;
                            float vyy = zeta - eta * v.y * v.y;
                            z = max(0, v.y + vxx);
                            w[0] = z * z;
                            z = max(0, -v.x + vyy);
                            w[2] = z * z;
                            z = max(0, -v.y + vxx);
                            w[4] = z * z;
                            z = max(0, v.x + vyy);
                            w[6] = z * z;
                            // Odd sectors: the same polynomials on v rotated by 45 degrees.
                            float2 d = 0.70710678 * float2(v.x - v.y, v.x + v.y);
                            vxx = zeta - eta * d.x * d.x;
                            vyy = zeta - eta * d.y * d.y;
                            z = max(0, d.y + vxx);
                            w[1] = z * z;
                            z = max(0, -d.x + vyy);
                            w[3] = z * z;
                            z = max(0, -d.y + vxx);
                            w[5] = z * z;
                            z = max(0, d.x + vyy);
                            w[7] = z * z;
                            float weightSum = w[0] + w[1] + w[2] + w[3] + w[4] + w[5] + w[6] + w[7];

                            float g = exp(-3.125 * dot(v, v)) / max(weightSum, 1e-8);
                            [unroll] for (int sector = 0; sector < 8; ++sector)
                            {
                                float wk = w[sector] * g;
                                m[sector] += float4(c * wk, wk);
                                s[sector] += cc * wk;
                            }
                        }
                    }
                }

                // Blend the sector means, favouring the low-variance (flat paint) sectors.
                float4 o = 0;
                [unroll] for (int k = 0; k < 8; ++k)
                {
                    float mw = max(m[k].w, 1e-8);
                    float3 mean = m[k].rgb / mw;
                    float3 variance = abs(s[k] / mw - mean * mean);
                    float sigma2 = variance.r + variance.g + variance.b;
                    float wk = 1 / (1 + pow(max(1000 * sigma2, 0), 0.5 * q));
                    o += float4(mean * wk, wk);
                }

                float centreDepth = SAMPLE_TEXTURE2D_X_LOD(_WS_PaintSrc, sampler_PointClamp, uv, 0).a;
                return float4(o.rgb / max(o.w, 1e-8), centreDepth);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PaintComposite"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPaintComposite
            #pragma target 3.5

            // Paint: strength, depth sigma (m), unused, unused.
            float4 _WS_Paint;

            // Full-res composite: joint-bilateral upsample of the painted half-res image (depth-aware, so paint never
            // bleeds across silhouettes), blended over the LDR part of the source; the HDR excess is kept for bloom.
            float4 FragPaintComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 src = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                float raw = SAMPLE_TEXTURE2D_X_LOD(_WorldStyleDepth, sampler_PointClamp, uv, 0).r;
                float eyeDepth = LinearEyeDepth(raw, _ZBufferParams);

                // The 4 nearest half-res texels (clamped loads) with their bilinear weights.
                int2 halfMax = int2(_WS_HalfTexel.zw) - 1;
                float2 halfPos = uv * _WS_HalfTexel.zw - 0.5;
                float2 cellPos = floor(halfPos);
                float2 fracPos = halfPos - cellPos;
                int2 p0 = clamp(int2(cellPos), 0, halfMax);
                int2 p1 = clamp(int2(cellPos) + 1, 0, halfMax);
                float4 t00 = LOAD_TEXTURE2D_X(_WS_Painted, int2(p0.x, p0.y));
                float4 t10 = LOAD_TEXTURE2D_X(_WS_Painted, int2(p1.x, p0.y));
                float4 t01 = LOAD_TEXTURE2D_X(_WS_Painted, int2(p0.x, p1.y));
                float4 t11 = LOAD_TEXTURE2D_X(_WS_Painted, int2(p1.x, p1.y));
                float4 bilinear = float4((1 - fracPos.x) * (1 - fracPos.y), fracPos.x * (1 - fracPos.y),
                                         (1 - fracPos.x) * fracPos.y, fracPos.x * fracPos.y);

                // Depth similarity; fall back to plain bilinear when no neighbour matches the full-res depth.
                float4 depthWeight = exp(-abs(float4(t00.a, t10.a, t01.a, t11.a) - eyeDepth) / max(_WS_Paint.y, 1e-3));
                float4 weights = bilinear * depthWeight;
                float weightSum = dot(weights, 1);
                bool fallback = weightSum < 1e-4;
                weights = fallback ? bilinear : weights;
                weightSum = fallback ? 1.0 : weightSum;
                float3 p = (t00.rgb * weights.x + t10.rgb * weights.y + t01.rgb * weights.z + t11.rgb * weights.w) / weightSum;

                float3 painted = p * p; // gamma 2 back to linear
                float3 base = min(src.rgb, 1.0);
                float3 excess = src.rgb - base;
                return float4(lerp(base, painted, _WS_Paint.x) + excess, src.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
