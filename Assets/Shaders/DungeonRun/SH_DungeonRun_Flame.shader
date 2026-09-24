Shader "DungeonRun/SH_DungeonRun_Flame"
{
    Properties
    {
        [HDR] _CoreColor("Core", Color) = (4,3.2,1.6,1)
        [HDR] _MidColor("Mid", Color) = (3.2,1.3,0.3,1)
        [HDR] _OuterColor("Outer", Color) = (1.6,0.35,0.06,1)
        _Intensity("Intensity", Range(0,4)) = 1
        _Opacity("Outer Opacity", Range(0,1)) = 0.75
        _Speed("Rise Speed", Range(0,4)) = 1.6
        _Distortion("Distortion", Range(0,0.5)) = 0.18
        _Sway("Sway", Range(0,0.3)) = 0.06
        _EdgeSoftness("Painted Edge Softness", Range(0.01,0.3)) = 0.08
        [ToggleUI] _HaloMode("Halo Mode (soft glow card)", Float) = 0
        _HaloPower("Halo Falloff", Range(0.5,6)) = 2.2

        // Driven per renderer by DungeonRunFlameLight through a MaterialPropertyBlock; declared here so the
        // UnityPerMaterial layout is fixed.
        [HideInInspector] _FlameFlicker("Flame Flicker", Float) = 1
        [HideInInspector] _FlameSeed("Flame Seed", Float) = 0

        [Header(Painted Flame Shape)]
        _FlameHeight("Flame Height (card fraction)", Range(0.3,0.95)) = 0.78
        _FlameWidth("Flame Half Width (card fraction)", Range(0.08,0.34)) = 0.25
        _LickRate("Tongue Lick Rate (Hz)", Range(0,6)) = 2.6
        _Wisps("Detached Wisps", Range(0,1)) = 1
        _CoreMaxRatio("Core Max Luminance (x Mid)", Range(1,4)) = 2.5
        _HueSafeHDR("Hue-Safe HDR (keep painted hues on screen)", Range(0,1)) = 1
        _BodyLift("Body Hue Shift toward Core", Range(0,1)) = 0.5
        _DepthOffset("Camera-ward Depth Offset (m)", Range(0,2)) = 0.6
    }
    SubShader
    {
        // Billboards read the pivot from the object matrix, so static/dynamic batching must not merge them.
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"
               "IgnoreProjector"="True" "DisableBatching"="True" }

        Pass
        {
            Name "Flame"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FlameVertex
            #pragma fragment FlameFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _MidColor;
                float4 _OuterColor;
                float _Intensity;
                float _Opacity;
                float _Speed;
                float _Distortion;
                float _Sway;
                float _EdgeSoftness;
                float _HaloMode;
                float _HaloPower;
                float _FlameFlicker;
                float _FlameSeed;
                float _FlameHeight;
                float _FlameWidth;
                float _LickRate;
                float _Wisps;
                float _CoreMaxRatio;
                float _HueSafeHDR;
                float _BodyLift;
                float _DepthOffset;
            CBUFFER_END
            // x = 1 when a deterministic capture override is active, y = override time in seconds.
            float4 _DR_FlameTime;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float phase : TEXCOORD1; // per-instance [0,1), identical on every vertex of the card
                // Per-instance, time-only motion signals (identical on every vertex, so constant over the card):
                // lick = main / left / right tongue height licks + lick-wave amplitude;
                // lean = main / left / right tongue leans + lateral jitter.
                float4 lick : TEXCOORD2;
                float4 lean : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float DRFlameHash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float DRFlameHash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float DRFlameHash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Smooth 1D value noise in [0,1]; used as a time signal for the 2-4 Hz tongue licks.
            float DRFlameNoise1(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3 - 2 * f);
                return lerp(DRFlameHash11(i), DRFlameHash11(i + 1), u);
            }

            // Smooth value noise in [0,1] (hashed lattice, smoothstep interpolation).
            float DRFlameNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                float a = DRFlameHash12(i);
                float b = DRFlameHash12(i + float2(1, 0));
                float c = DRFlameHash12(i + float2(0, 1));
                float d = DRFlameHash12(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Polynomial smooth maximum: soft union of two inside-positive fields, so tongues merge like wet paint.
            float DRFlameSmoothMax(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (a - b) / k);
                return lerp(b, a, h) + k * h * (1 - h);
            }

            // Teardrop half-width profile over normalised height v in [0,1]: rounded base (sqrt), tapered pointed
            // tip; normalised to peak 1 near v = 0.29.
            float DRTeardrop(float v)
            {
                float vc = saturate(v);
                return sqrt(vc) * pow(max(1 - vc, 1e-5), 1.25) * 2.85;
            }

            // One flame tongue: signed horizontal distance in card uv (> 0 inside) to a leaning teardrop that starts
            // at (rootX, baseY), is `height` tall and `halfWidth` wide; its centre line bends out to rootX + lean at
            // the tip. Outside its height range the field keeps falling with the vertical distance, so it never
            // leaves a zero-width seam on the centre line.
            float DRFlameTongue(float x, float y, float rootX, float baseY, float height, float halfWidth, float lean)
            {
                float s = (y - baseY) / height;
                float sc = saturate(s);
                float centre = rootX + lean * sc * sqrt(sc);
                float inside = halfWidth * DRTeardrop(sc) - abs(x - centre);
                return inside - (max(-s, 0) + max(s - 1, 0)) * height;
            }

            // Horizontal bend of the flame at height y: slow sway + licking wave travelling up + lateral jitter, all
            // growing toward the tip. bend = (sway amount, wave phase, wave amplitude, jitter amount).
            float DRFlameBend(float y, float flameH, float4 bend)
            {
                float yn = saturate(y / flameH);
                return bend.x * yn * yn + sin(y * 8.0 - bend.y) * bend.z * yn + bend.w * yn * sqrt(yn);
            }

            // Detached wisp: a small upward tongue that pinches off a tip at (tipX, tipY), rises by `travel`, drifts
            // to a random side (`drift` in [0,1), new per cycle), shrinks and fades over its cycle in [0,1). x is the
            // unbent card coordinate: the wisp moves rigidly with the bend at its own centre, so the height-varying
            // wave never shears it flat.
            float DRFlameWisp(float x, float y, float flameH, float4 bend, float cycle, float drift, float tipX,
                              float tipY, float halfWidth, float travel, out float fade)
            {
                float w = halfWidth * (1 - 0.7 * cycle);
                float h = w * 2.8;
                float baseY = tipY - h * 0.6 + cycle * travel;
                float side = (drift - 0.5) * 2;
                float cx = tipX + side * halfWidth * 1.5 * cycle;
                fade = smoothstep(0.0, 0.12, cycle) * (1 - cycle * cycle);
                // Fade out before the wisp reaches the card's top fade, so it never shows a flat clipped top.
                fade *= saturate((0.93 - (baseY + h)) * 12.5);
                float xr = x - DRFlameBend(baseY + h * 0.35, flameH, bend);
                return DRFlameTongue(xr, y, cx, baseY, h, w, side * w * 0.5);
            }

            // Hue-safe HDR. The lab camera has no tonemapper, so every channel above 1 clips on screen and bright
            // painted tones collapse to yellow or white. The displayed part is divided by peak^amount (1 = exactly the
            // painted hue on screen) and the removed energy goes to the dominant channel(s) only, which clip anyway:
            // Bloom still sees the full peak brightness. amount = 0 returns the raw HDR colour.
            float3 DRFlameHueSafe(float3 c, float amount)
            {
                float peak = max(max(c.r, c.g), max(c.b, 1e-4));
                float compress = pow(max(peak, 1), -amount);
                float3 dominant = smoothstep(0.92, 1.0, c / peak);
                return c * compress + (peak - peak * compress) * dominant;
            }

            // Moves the hue of `from` toward the hue of `toward` by t, keeping from's min/max channels (its
            // saturation and brightness): lerps the min-max normalised chroma shapes, then rebuilds.
            float3 DRFlameHueShift(float3 from, float3 toward, float t)
            {
                float fromMax = max(max(from.r, from.g), from.b);
                float fromMin = min(min(from.r, from.g), from.b);
                float towardMax = max(max(toward.r, toward.g), toward.b);
                float towardMin = min(min(toward.r, toward.g), toward.b);
                float3 fromShape = (from - fromMin) / max(fromMax - fromMin, 1e-4);
                float3 towardShape = (toward - towardMin) / max(towardMax - towardMin, 1e-4);
                return fromMin + (fromMax - fromMin) * lerp(fromShape, towardShape, t);
            }

            Varyings FlameVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float3 pivotWS = objectToWorld._m03_m13_m23;
                float scaleX = length(objectToWorld._m00_m10_m20);
                float scaleY = length(objectToWorld._m01_m11_m21);
                // View matrix rows are the camera basis in world space: row 0 right, row 1 up, row 2 back.
                float3 cameraRight = UNITY_MATRIX_V._m00_m01_m02;
                float3 cameraUp = UNITY_MATRIX_V._m10_m11_m12;

                float3 positionWS;
                if (_HaloMode > 0.5)
                {
                    // Full camera-facing glow card centred on the pivot; size from the X scale.
                    positionWS = pivotWS + (cameraRight * input.positionOS.x + cameraUp * (input.positionOS.y - 0.5)) * scaleX;
                }
                else
                {
                    // Cylindrical (world Y) billboard: faces the camera horizontally and stays upright.
                    float3 toCamera = IsPerspectiveProjection() ? GetCameraPositionWS() - pivotWS : UNITY_MATRIX_V._m20_m21_m22;
                    float3 right = cross(float3(toCamera.x, 0, toCamera.z), float3(0, 1, 0));
                    right = dot(right, right) > 1e-8 ? right : float3(cameraRight.x, 0, cameraRight.z);
                    right *= rsqrt(max(dot(right, right), 1e-8));
                    positionWS = pivotWS + right * (input.positionOS.x * scaleX) + float3(0, input.positionOS.y * scaleY, 0);
                }

                // Pull the card toward the camera so it never cuts into the wall or pillar right behind its fixture.
                float3 towardCamera = IsPerspectiveProjection() ? GetCameraPositionWS() - pivotWS : UNITY_MATRIX_V._m20_m21_m22;
                positionWS += towardCamera * (rsqrt(max(dot(towardCamera, towardCamera), 1e-8)) * _DepthOffset);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                float phase = DRFlameHash13(pivotWS * 0.731 + _FlameSeed * 0.1379);
                output.phase = phase;

                // Time-only signals are evaluated once per vertex instead of per pixel. Tongue heights lick at
                // ~2.5-3.5 Hz and leans drift at ~1 Hz (1D value noise), so the silhouette changes clearly between
                // frames 0.1 s apart while the whole flame still sways slowly.
                float time = _DR_FlameTime.x > 0.5 ? _DR_FlameTime.y : _Time.y;
                float lickTime = time * _LickRate;
                output.lick = float4(DRFlameNoise1(lickTime * 1.1 + phase * 11.0),
                                     DRFlameNoise1(lickTime * 1.3 + phase * 17.0 + 3.7),
                                     DRFlameNoise1(lickTime * 0.95 + phase * 29.0 + 8.1),
                                     0.4 + 0.6 * DRFlameNoise1(time * 0.8 + phase * 23.0));
                output.lean = float4(DRFlameNoise1(time * 0.9 + phase * 13.0 + 5.3),
                                     DRFlameNoise1(time * 1.3 + phase * 5.0 + 1.9),
                                     DRFlameNoise1(time * 1.1 + phase * 7.0 + 6.3),
                                     DRFlameNoise1(lickTime * 1.35 + phase * 41.0) - 0.5);
                return output;
            }

            float4 HaloFragment(float2 uv, float2 offset, float time, float flicker)
            {
                float2 d = uv - 0.5;
                float len = length(d);
                float2 dir = d / max(len, 1e-4);
                // Mild noise wobble (continuous around the circle: sampled on the direction, not the angle) plus slow
                // blotches; together they scale the radius by at most +-4.5%.
                float wobble = DRFlameNoise(dir * 2.3 + float2(time * 0.9, -time * 0.6) + offset);
                float blotch = DRFlameNoise(uv * 4.0 + offset * 0.37 + float2(0, -time * 0.25));
                // Falloff radius 0.46 uv: with the bounded wobble the glow is exactly 0 for len >= 0.46 / 0.955 (0.482),
                // inside the card border (0.5), so neither a disc nor the square card edge can show.
                float r = len / 0.46 * (1 + (wobble - 0.5) * 0.06 + (blotch - 0.5) * 0.03);
                float g = pow(saturate(1 - r), _HaloPower);
                // Painted value steps only in the bright inner glow; the outer falloff stays a smooth gradient that
                // reaches 0 with zero slope (no ring where the first step would rise).
                float s = g * 3;
                float stepped = (floor(s) + smoothstep(0.3, 0.7, frac(s))) / 3;
                float halo = lerp(g, stepped, 0.6 * smoothstep(0.3, 0.6, g));
                halo *= 1 - smoothstep(0.43, 0.485, len);
                // Alpha 0: purely additive through the premultiplied blend.
                return float4(_OuterColor.rgb * _Intensity * flicker * halo, 0);
            }

            float4 FlameFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Screen-space size of one pixel in card uv, taken before any branch; it bounds the painted edge
                // width from below so the crisp edges stay antialiased.
                float2 pixelUV = fwidth(input.uv);

                float time = _DR_FlameTime.x > 0.5 ? _DR_FlameTime.y : _Time.y;
                float flicker = clamp(_FlameFlicker, 0.2, 2.0);
                float phase = input.phase;
                float2 offset = phase * float2(37.13, 17.91);
                float phaseAngle = phase * 6.2831853;

                [branch] if (_HaloMode > 0.5)
                    return HaloFragment(input.uv, offset, time, flicker);

                float rise = time * _Speed;        // upward scroll of turbulence, breakup and brush strokes
                float lickTime = time * _LickRate; // fast tongue motion, in cycles
                // Nominal flame height as a fraction of the card: taller when the flicker is above 1; capped so the
                // tallest licks and wisps stay below the card's top fade.
                float flameH = clamp(_FlameHeight * pow(flicker, 0.9), 0.2, 0.84);
                float w = _FlameWidth;
                float x = input.uv.x - 0.5;
                float y = input.uv.y;
                float yn = saturate(y / flameH);

                // --- Motion, applied to x so every tongue and tonal layer moves together ---
                // Slow sway; a licking wave travelling up the flame at _LickRate Hz (bends the tongues into moving
                // S-curves) with a slowly varying amplitude; a 3-4 Hz lateral jitter of the upper flame.
                float4 bend = float4((sin(time * 1.7 + phaseAngle) + 0.45 * sin(time * 0.63 + phaseAngle * 3.0)) * _Sway,
                                     lickTime * 6.2831853 - phaseAngle * 2.0,
                                     _Distortion * 0.4 * input.lick.w,
                                     input.lean.w * _Distortion * 0.5);
                // Two-octave value noise scrolling upward; its horizontal distortion grows with height.
                float2 np = float2(x * 3.4, y * 2.8 - rise * 1.1) + offset;
                float turb = DRFlameNoise(np) * 0.62 + DRFlameNoise(np * 2.17 + 5.7) * 0.38;
                float xw = x - DRFlameBend(y, flameH, bend) - (turb - 0.5) * 2.0 * _Distortion * yn * sqrt(yn);

                // --- Three licking tongues: main body + two asymmetric side tongues ---
                // Height licks and leans come from the vertex stage; per-instance asymmetry decides which side
                // tongue is larger.
                float asymL = frac(phase * 7.31 + 0.17);
                float asymR = frac(phase * 3.77 + 0.61);

                float baseM = 0.015;
                float hM = flameH * (0.86 + 0.16 * input.lick.x);
                float leanMx = w * 0.5 * (input.lean.x - 0.5);
                float dM = DRFlameTongue(xw, y, 0, baseM, hM, w, leanMx);

                float sizeL = lerp(0.85, 1.12, asymL);
                float wL = w * 0.55 * sizeL;
                float baseL = flameH * 0.06;
                float hL = flameH * lerp(0.45, 0.78, input.lick.y) * sizeL;
                float rootL = -w * 0.38;
                float leanLx = -w * lerp(0.35, 0.95, input.lean.y);
                float dL = DRFlameTongue(xw, y, rootL, baseL, hL, wL, leanLx);

                float sizeR = lerp(0.85, 1.12, asymR);
                float wR = w * 0.5 * sizeR;
                float baseR = flameH * 0.12;
                float hR = flameH * lerp(0.4, 0.7, input.lick.z) * sizeR;
                float rootR = w * 0.36;
                float leanRx = w * lerp(0.3, 0.9, input.lean.z);
                float dR = DRFlameTongue(xw, y, rootR, baseR, hR, wR, leanRx);

                // Upward-scrolling breakup, stretched vertically: it slits the upper flame so tips split into prongs
                // that travel up and pinch off, instead of being cut flat.
                float2 cp = float2(xw * 6.5, y * 2.4 - rise * 1.2) + offset * 1.3;
                float cut = DRFlameNoise(cp) * 0.6 + DRFlameNoise(cp * 2.3 + 3.1) * 0.4;
                float carve = max(cut - 0.42, 0) * 0.5 * smoothstep(0.35, 0.95, yn);
                float k = w * 0.12;
                float d = DRFlameSmoothMax(dM, DRFlameSmoothMax(dL, dR, k), k) - carve;
                float dMain = dM - carve;

                // Detached wisps: one pinches off the main tip, one off a side tongue (side chosen per instance). They
                // start from the mean tip height so they rise independently of the fast height licks.
                float fade0, fade1;
                float c0 = rise * 0.8 + phase * 3.1;
                float dW0 = DRFlameWisp(x, y, flameH, bend, frac(c0), DRFlameHash11(floor(c0) + phase * 91.7), leanMx,
                                        baseM + flameH * 0.94, w * 0.22, flameH * 0.24, fade0);
                float sideSel = step(0.5, frac(phase * 5.3));
                float c1 = rise * 0.62 + phase * 5.9 + 0.5;
                float tipX = lerp(rootL + leanLx, rootR + leanRx, sideSel);
                float tipY = lerp(baseL + flameH * 0.6 * sizeL, baseR + flameH * 0.55 * sizeR, sideSel);
                float dW1 = DRFlameWisp(x, y, flameH, bend, frac(c1), DRFlameHash11(floor(c1) + phase * 53.3), tipX,
                                        tipY, lerp(wL, wR, sideSel) * 0.4, flameH * 0.2, fade1);

                // --- Painted edges: brush strokes run along the flame, so each layer boundary reads as the ragged end
                // of an upward stroke (stroke-scale notches + bristle serration that grows into dry-brush breakup at
                // the tips), with a narrow transition. ---
                float strokeA = DRFlameNoise(float2(xw * 11.0, y * 2.4 - rise * 0.95) + offset * 1.7);
                float strokeB = DRFlameNoise(float2(xw * 13.0 + 3.1, y * 1.9 - rise * 1.05) + offset * 2.3);
                float bristle = DRFlameNoise(float2(xw * 38.0, y * 6.0 - rise * 1.2) + offset * 0.9);
                float e = max(_EdgeSoftness * 0.08, pixelUV.x * 0.6);

                // Tonal layers nest inside the outer silhouette: the body fills the lower ~2/3 (and the side tongue
                // roots) and pushes upward streaks into the outer lick; the core sits low inside the main tongue only
                // and its top breaks into upward stroke prongs instead of a smooth candle teardrop.
                float dOuter = d + (strokeA - 0.5) * 0.05 + (bristle - 0.5) * lerp(0.012, 0.035, yn);
                float dBody = d - w * (0.2 + 0.55 * yn * sqrt(yn)) + (strokeB - 0.5) * 0.08 + (bristle - 0.5) * 0.014;
                float dCore = dMain - w * (0.42 + 0.95 * yn) + (strokeA - 0.5) * 0.05
                            + (strokeB - 0.5) * 0.14 * smoothstep(0.05, 0.35, yn);
                // The turbulence stretches the fields horizontally (up to ~10x where it compresses x), which would
                // widen those edges into blurry gradients: rescale by the screen-space gradient (fields are in
                // horizontal card uv, so one pixel should change them by pixelUV.x) so every painted edge keeps the
                // same narrow width. The halo branch above is uniform per material, so the derivatives are well
                // defined.
                float fieldScale = pixelUV.x / max(length(float2(ddx(d), ddy(d))), 0.3 * pixelUV.x);
                dOuter *= fieldScale;
                dBody *= fieldScale;
                dCore *= fieldScale;

                // The card fade keeps tongues, wisps and sway from clipping against the quad border.
                float cardFade = smoothstep(1.0, 0.93, y) * smoothstep(0.5, 0.44, abs(x)) * smoothstep(0.0, 0.02, y);
                float wisp = max(smoothstep(-e, e, dW0) * fade0, smoothstep(-e, e, dW1) * fade1) * _Wisps;
                float outer = max(smoothstep(-e, e, dOuter), wisp) * cardFade;
                float body = smoothstep(-e, e, dBody) * outer;
                float core = smoothstep(-e, e, dCore) * body;

                // --- Painted tones ---
                // Outer lick: flat patches of red-orange and orange (hard-edged dabs), shifting to a saturated ember
                // hue at the tongue tips and wisps. Core: warm cream, its luminance capped at _CoreMaxRatio x the
                // body's so it never outshines the body into a white blob. Body: the mid colour with its hue moved
                // toward the core's by _BodyLift while keeping its own saturation and brightness, so it reads as a
                // saturated yellow-orange (warm) or light blue (cold) between the outer lick and the core.
                float3 lumaWeights = float3(0.2126, 0.7152, 0.0722);
                float3 outerCol = _OuterColor.rgb;
                float outerPeak = max(max(outerCol.r, outerCol.g), max(outerCol.b, 1e-4));
                float3 emberCol = outerCol * outerCol / outerPeak;
                float coreLuma = max(dot(_CoreColor.rgb, lumaWeights), 1e-4);
                float3 coreCol = _CoreColor.rgb * min(1.0, _CoreMaxRatio * dot(_MidColor.rgb, lumaWeights) / coreLuma);
                float3 bodyCol = DRFlameHueShift(_MidColor.rgb, coreCol, _BodyLift);
                float3 bandCol = lerp(outerCol, bodyCol, 0.4);
                float dabA = smoothstep(0.46, 0.54, DRFlameNoise(float2(xw * 7.0, y * 3.0 - rise * 0.8) + offset * 0.5 + 11.0));
                float ember = smoothstep(0.6, 1.0, yn) * 0.75;

                float3 colour = lerp(outerCol, bandCol, dabA * 0.65);
                colour = lerp(colour, emberCol, ember);
                colour = lerp(colour, bodyCol, body);
                colour = lerp(colour, coreCol, core);

                // HDR with the painted hue kept on screen, then the painted value structure (outer lick darker than
                // the body, ember tips darker still) and paint texture (bristle striations, dab-to-dab value). These
                // are applied after the mapping, which would otherwise normalise them away.
                colour = DRFlameHueSafe(colour * _Intensity * flicker, _HueSafeHDR);
                float value = lerp(lerp(0.86, 0.62, ember), 1.0, body);
                colour *= value * (1 + (bristle - 0.5) * 0.2 + (strokeB - 0.5) * 0.12 * body - (1 - dabA) * 0.06 * (1 - body));

                // Premultiplied: body and core are fully opaque, the outer lick lets a little background through.
                float alpha = outer * lerp(_Opacity, 1.0, body);
                return float4(colour * outer, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
