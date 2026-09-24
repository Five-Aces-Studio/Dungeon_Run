using System;

namespace DungeonRun.Rendering
{
    /// <summary>
    /// Deterministic, exactly-tileable acrylic stroke map, baked on the CPU with no engine dependency. Opaque brush
    /// strokes are painted back to front (painter's algorithm, every position wrapped modulo the tile size), broad washes
    /// before medium strokes and small touches. Each stroke lands full with a blunt end, tapers and bends slightly along
    /// its length and lifts off in a ragged dry-brush tail where single bristle lines run out of paint.
    /// R = stroke value (flat per stroke, centre-weighted with rare accents), G = stroke hue id (1..255,
    /// 0 = never painted), B = low-contrast bristle streaks with a darker edge ridge (centred), A = low-frequency soft
    /// blotch (centred), independent of the strokes.
    /// </summary>
    public static class PaintStrokeBaker
    {
        // Medium stroke geometry as fractions of the tile size; washes and small touches scale it.
        private const float MinLength = 0.085f, MaxLength = 0.14f;
        private const float MinWidth = 0.03f, MaxWidth = 0.045f;
        private const float MediumMinScale = 0.8f, MediumMaxScale = 1.25f;
        private const float WashMinScale = 2f, WashMaxScale = 3f;
        private const float SmallMinLengthScale = 0.45f, SmallMaxLengthScale = 0.7f;
        private const float SmallMinWidthScale = 0.5f, SmallMaxWidthScale = 0.75f;

        // Stroke direction: dominant horizontal with a narrow spread; a minority of strokes cross at steeper angles.
        // Either end may be the landing end.
        private const float NarrowSpread = 22f * (float)Math.PI / 180f;
        private const float WideSpread = 60f * (float)Math.PI / 180f;
        private const float WideFraction = 0.15f;

        // Stroke shape: centreline bend (end deflection / half length), width lost from landing to lift-off, blunt
        // landing cap length (x half width, flat-brush superellipse) and a slight wobble of the edges.
        private const float MaxBend = 0.14f;
        private const float MinTaper = 0.3f, MaxTaper = 0.55f;
        private const float CapLength = 0.8f;
        private const float EdgeWobble = 0.04f;

        // Dry-brush lift-off, as fractions of the stroke length: bristles start running out after 1 - DryLength (so a
        // stroke is always fully loaded through its middle); DryFloor = earliest bristle keeps this share of the dry
        // zone, DryEdgeBias = outer bristles run out sooner, DryDetail = fine bristle lines per streak track,
        // DryScumble = paint thinning along the streaks just before a bristle runs out.
        private const float MinDryLength = 0.2f, MaxDryLength = 0.4f;
        private const float DryFloor = 0.35f;
        private const float DryEdgeBias = 0.35f;
        private const float DryDetail = 2.7f;
        private const float DryScumble = 0.6f;

        // Underpainting: one medium stroke per jittered cell (1/16 x 1/32 of the tile, ~1.5x coverage); then a few broad
        // washes and the top strokes (medium, some small; ~0.9x) at random positions; then coverage repair strokes
        // beneath the paint (~0.3-0.7x, more at larger sizes where finer dry-brush gaps resolve): ~2.7-3.2x in total.
        private const int GridX = 16, GridY = 32;
        private const float GridJitter = 0.35f;
        private const int WashCount = 16;
        private const int TopStrokeCount = 280;
        private const float SmallFraction = 0.25f;

        // Value (R): triangular spread around mid grey plus a gentle periodic field so neighbouring strokes differ by
        // small-to-moderate steps; rare accents (medium strokes only) reach both ends of the range. Washes stay close to
        // the mid value.
        private const float ValueSpread = 0.28f;
        private const float WashValueSpread = 0.16f;
        private const int ValueFieldFrequency = 4;
        private const float ValueFieldAmount = 0.12f;
        private const float AccentFraction = 0.06f;
        private const float AccentMin = 0.26f, AccentMax = 0.42f;
        private const float ValueBristleModulation = 0.05f;

        // Bristle (B): streaks and edge ridge, normalized to a fixed low contrast instead of the full range.
        private const int MinBristles = 6, MaxBristles = 10;
        private const float StreakStrength = 0.3f;
        private const float DryStreakBoost = 0.5f;
        private const float RidgeDarkening = 0.15f;
        private const float BristleStdDev = 0.085f;

        private const float PreFill = 0.5f;
        private const float Uncovered = -1f;

        private enum StrokeClass
        {
            Medium,
            Wash,
            Small,
        }

        /// <summary>size: power of two &gt;= 64 (else ArgumentException). Returns size*size*4 RGBA32 bytes, row-major, y up.</summary>
        public static byte[] BakeRgba32(int size, int seed)
        {
            if (size < 64 || (size & (size - 1)) != 0)
                throw new ArgumentException("size must be a power of two and at least 64.", nameof(size));

            int pixelCount = size * size;
            var canvas = new Canvas
            {
                Size = size,
                Value = new float[pixelCount],
                Hue = new float[pixelCount],
                Bristle = new float[pixelCount],
                Bare = new float[pixelCount],
                // Strokes are at most MaxLength * WashMaxScale = 0.42 of the tile long: one sample per pixel fits.
                WidthProfile = new float[size + 2],
                StreakProfile = new float[size + 2],
            };
            for (int p = 0; p < pixelCount; p++)
            {
                canvas.Value[p] = PreFill;
                canvas.Hue[p] = Uncovered;
                canvas.Bristle[p] = PreFill;
                canvas.Bare[p] = 1f;
            }

            var rng = new Pcg32(seed);

            // 1. Underpainting: jittered grid, painted in shuffled order so no row systematically overlaps the next.
            int cellCount = GridX * GridY;
            var order = new int[cellCount];
            for (int c = 0; c < cellCount; c++) order[c] = c;
            for (int c = cellCount - 1; c > 0; c--)
            {
                int swap = rng.NextInt(c + 1);
                int tmp = order[c];
                order[c] = order[swap];
                order[swap] = tmp;
            }
            float cellWidth = (float)size / GridX, cellHeight = (float)size / GridY;
            for (int c = 0; c < cellCount; c++)
            {
                int gx = order[c] % GridX, gy = order[c] / GridX;
                float cx = (gx + 0.5f + (rng.NextFloat() * 2f - 1f) * GridJitter) * cellWidth;
                float cy = (gy + 0.5f + (rng.NextFloat() * 2f - 1f) * GridJitter) * cellHeight;
                PaintDab(ref canvas, NewDab(ref rng, cx, cy, size, seed, StrokeClass.Medium), false);
            }

            // 2. Broad washes, then the top strokes, at random positions.
            for (int k = 0; k < WashCount; k++)
            {
                float cx = rng.NextFloat() * size, cy = rng.NextFloat() * size;
                PaintDab(ref canvas, NewDab(ref rng, cx, cy, size, seed, StrokeClass.Wash), false);
            }
            for (int k = 0; k < TopStrokeCount; k++)
            {
                var strokeClass = rng.NextFloat() < SmallFraction ? StrokeClass.Small : StrokeClass.Medium;
                float cx = rng.NextFloat() * size, cy = rng.NextFloat() * size;
                PaintDab(ref canvas, NewDab(ref rng, cx, cy, size, seed, strokeClass), false);
            }

            // 3. Coverage repair: any pixel no stroke has claimed gets a medium stroke centred on it, composited beneath
            // all existing paint (exactly as if it had been part of the underpainting), so it only shows through the
            // holes. A stroke always claims its own centre pixel with alpha > 0.5 (positive width there and no
            // dry-brush thinning before 1 - MaxDryLength > 0.5 of its length) and no paint above claimed that pixel,
            // so after this scan no hole can remain.
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (canvas.Hue[y * size + x] == Uncovered)
                        PaintDab(ref canvas, NewDab(ref rng, x + 0.5f, y + 0.5f, size, seed, StrokeClass.Medium), true);
                }
            }

            // 4. Safety net: an uncovered pixel would leak the pre-fill into the value channel.
            for (int p = 0; p < pixelCount; p++)
            {
                if (canvas.Hue[p] == Uncovered)
                    throw new InvalidOperationException($"Paint stroke coverage failed at pixel {p}.");
            }

            var blotch = new float[pixelCount];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    blotch[y * size + x] = Blotch(x, y, size, seed);

            var bytes = new byte[pixelCount * 4];
            NormalizeCentredInto(canvas.Value, bytes, 0);
            for (int p = 0; p < pixelCount; p++)
                bytes[p * 4 + 1] = (byte)(1 + Math.Min(254, (int)(canvas.Hue[p] * 255f)));
            NormalizeStdDevInto(canvas.Bristle, bytes, 2, BristleStdDev);
            NormalizeCentredInto(blotch, bytes, 3);
            return bytes;
        }

        private struct Canvas
        {
            public int Size;
            public float[] Value;
            public float[] Hue;
            public float[] Bristle;
            public float[] Bare; // share of the pre-fill still showing through the paint above it
            public float[] WidthProfile, StreakProfile; // per-stroke scratch, sampled along t at ~1 px spacing
        }

        private struct Dab
        {
            public float CentreX, CentreY;
            public float Cos, Sin;
            public float HalfLength, HalfWidth;
            public float Bend, Taper, CapEnd, DryLength;
            public float Value, Hue;
            public int Bristles;
            public float BristleOffset, AlongOffset;
            public int NoiseSeed;
        }

        private static Dab NewDab(ref Pcg32 rng, float centreX, float centreY, int size, int seed, StrokeClass strokeClass)
        {
            float lengthScale, widthScale;
            switch (strokeClass)
            {
                case StrokeClass.Wash:
                    lengthScale = widthScale = WashMinScale + rng.NextFloat() * (WashMaxScale - WashMinScale);
                    break;
                case StrokeClass.Small:
                    lengthScale = SmallMinLengthScale + rng.NextFloat() * (SmallMaxLengthScale - SmallMinLengthScale);
                    widthScale = SmallMinWidthScale + rng.NextFloat() * (SmallMaxWidthScale - SmallMinWidthScale);
                    break;
                default:
                    lengthScale = widthScale = MediumMinScale + rng.NextFloat() * (MediumMaxScale - MediumMinScale);
                    break;
            }

            // Washes are broad horizontal sweeps; only medium and small strokes may cross steeply.
            float spread = strokeClass != StrokeClass.Wash && rng.NextFloat() < WideFraction ? WideSpread : NarrowSpread;
            float angle = (rng.NextFloat() * 2f - 1f) * spread;
            if (rng.NextFloat() < 0.5f) angle += (float)Math.PI;
            float length = (MinLength + rng.NextFloat() * (MaxLength - MinLength)) * lengthScale * size;
            float width = (MinWidth + rng.NextFloat() * (MaxWidth - MinWidth)) * widthScale * size;
            float halfLength = 0.5f * length, halfWidth = 0.5f * width;

            float value;
            if (strokeClass == StrokeClass.Medium && rng.NextFloat() < AccentFraction)
            {
                float accent = AccentMin + rng.NextFloat() * (AccentMax - AccentMin);
                value = 0.5f + (rng.NextFloat() < 0.5f ? -accent : accent);
            }
            else
            {
                float field = PeriodicNoise(centreX / size * ValueFieldFrequency, centreY / size * ValueFieldFrequency,
                    ValueFieldFrequency, ValueFieldFrequency, seed ^ 0x2545F491);
                float triangular = rng.NextFloat() + rng.NextFloat() - 1f;
                float spreadAmount = strokeClass == StrokeClass.Wash ? WashValueSpread : ValueSpread;
                value = 0.5f + ValueFieldAmount * field + spreadAmount * triangular;
            }

            int bristles = (int)Math.Round((MinBristles + rng.NextFloat() * (MaxBristles - MinBristles)) * widthScale);
            return new Dab
            {
                CentreX = centreX,
                CentreY = centreY,
                Cos = (float)Math.Cos(angle),
                Sin = (float)Math.Sin(angle),
                HalfLength = halfLength,
                HalfWidth = halfWidth,
                Bend = (rng.NextFloat() * 2f - 1f) * MaxBend * halfLength,
                Taper = MinTaper + rng.NextFloat() * (MaxTaper - MinTaper),
                CapEnd = Math.Min(0.25f, CapLength * halfWidth / length),
                DryLength = MinDryLength + rng.NextFloat() * (MaxDryLength - MinDryLength),
                Value = value,
                Hue = rng.NextFloat(),
                Bristles = Math.Max(4, Math.Min(24, bristles)),
                BristleOffset = rng.NextFloat() * 64f,
                AlongOffset = rng.NextFloat() * 64f,
                NoiseSeed = (int)rng.NextUInt(),
            };
        }

        // One brush stroke in stroke space: t runs 0 (landing) to 1 (lift-off) along the bent centreline, u runs -1..1
        // across the local width. ~1 px antialiased edge from the across distance and the bristle run-out points.
        // underneath: composite below all existing paint, i.e. only into the share of the pre-fill still showing.
        private static void PaintDab(ref Canvas canvas, in Dab dab, bool underneath)
        {
            int size = canvas.Size;
            int mask = size - 1;
            float length = 2f * dab.HalfLength;
            float absCos = Math.Abs(dab.Cos), absSin = Math.Abs(dab.Sin);
            float acrossExtent = dab.HalfWidth * (1f + EdgeWobble) + Math.Abs(dab.Bend);
            float extentX = absCos * dab.HalfLength + absSin * acrossExtent + 1.5f;
            float extentY = absSin * dab.HalfLength + absCos * acrossExtent + 1.5f;
            int x0 = (int)Math.Floor(dab.CentreX - extentX), x1 = (int)Math.Ceiling(dab.CentreX + extentX);
            int y0 = (int)Math.Floor(dab.CentreY - extentY), y1 = (int)Math.Ceiling(dab.CentreY + extentY);
            float dryStart = 1f - dab.DryLength;

            // Everything that depends only on the position along the stroke, sampled once per stroke at ~1 px spacing:
            // width (full where the loaded brush lands behind a blunt cap, narrowing as the pressure eases, slight
            // edge wobble) and the slow drift of the bristle streak strength.
            int samples = (int)Math.Ceiling(length) + 1;
            float[] widthProfile = canvas.WidthProfile, streakProfile = canvas.StreakProfile;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float width = dab.HalfWidth * (1f - dab.Taper * t * (float)Math.Sqrt(t)) *
                    (1f + EdgeWobble * ValueNoise1D(t * 5f + dab.AlongOffset, dab.NoiseSeed ^ 0x68E31DA4));
                if (t < dab.CapEnd)
                {
                    float c = 1f - t / dab.CapEnd;
                    width *= (float)Math.Sqrt(1f - c * c * c * c);
                }
                widthProfile[i] = width;
                streakProfile[i] = 0.65f + 0.35f * ValueNoise1D(t * 3f + dab.AlongOffset, dab.NoiseSeed ^ 0x5BD1E995);
            }

            for (int py = y0; py <= y1; py++)
            {
                float dy = py + 0.5f - dab.CentreY;
                int row = (py & mask) * size;
                for (int px = x0; px <= x1; px++)
                {
                    float dx = px + 0.5f - dab.CentreX;
                    float s = (dx * dab.Cos + dy * dab.Sin) / dab.HalfLength;
                    if (s <= -1f || s >= 1f) continue;

                    float t = 0.5f * (s + 1f);
                    float across = -dx * dab.Sin + dy * dab.Cos - dab.Bend * s * s;

                    float sample = t * samples;
                    int i0 = Math.Min((int)sample, samples - 1);
                    float blend = sample - i0;
                    float width = widthProfile[i0] + (widthProfile[i0 + 1] - widthProfile[i0]) * blend;
                    float alpha = Clamp01(width - Math.Abs(across) + 0.5f);
                    if (alpha <= 0f) continue;

                    // Bristle tracks follow the stroke; their strength drifts slowly along it.
                    float u = across / Math.Max(width, 0.5f);
                    float track = (u * 0.5f + 0.5f) * dab.Bristles + dab.BristleOffset;
                    float streak = ValueNoise1D(track, dab.NoiseSeed) *
                        (streakProfile[i0] + (streakProfile[i0 + 1] - streakProfile[i0]) * blend);

                    // Dry-brush lift-off: each fine bristle line runs out of paint at its own point (outer ones first),
                    // so the tail breaks up along the stroke direction instead of ending in a clean cap. Skipped where
                    // even the earliest possible run-out is still more than half a pixel ahead (fully loaded brush).
                    float dry = 0f;
                    float earliestRunOut = 1f - dab.DryLength * (1f + DryEdgeBias * u * u);
                    if ((earliestRunOut - t) * length < 0.5f)
                    {
                        float fine = 0.5f + 0.32f * ValueNoise1D(track * DryDetail + 17.3f, dab.NoiseSeed ^ 0x27D4EB2F) +
                            0.18f * ValueNoise1D(track * DryDetail * 2.3f + 5.1f, dab.NoiseSeed ^ 0x165667B1);
                        float runOut = 1f - dab.DryLength * (DryFloor + (1f - DryFloor) * fine + DryEdgeBias * u * u);
                        alpha *= Clamp01((runOut - t) * length + 0.5f);
                        if (alpha <= 0f) continue;
                        if (t > dryStart) dry = SmoothStep(dryStart, Math.Max(runOut, dryStart + 1e-3f), t);
                        alpha *= 1f - DryScumble * dry * (0.5f - 0.5f * streak);
                    }

                    float ridge = SmoothStep(0.6f, 0.95f, Math.Abs(u));
                    int index = row + (px & mask);
                    float value = dab.Value + ValueBristleModulation * streak;
                    float bristle = 0.5f + StreakStrength * streak * (1f + DryStreakBoost * dry) - RidgeDarkening * ridge;
                    float bare = canvas.Bare[index];
                    if (underneath)
                    {
                        float visible = alpha * bare;
                        canvas.Value[index] += (value - PreFill) * visible;
                        canvas.Bristle[index] += (bristle - PreFill) * visible;
                        if (alpha > 0.5f && canvas.Hue[index] == Uncovered) canvas.Hue[index] = dab.Hue;
                    }
                    else
                    {
                        canvas.Value[index] += (value - canvas.Value[index]) * alpha;
                        canvas.Bristle[index] += (bristle - canvas.Bristle[index]) * alpha;
                        if (alpha > 0.5f) canvas.Hue[index] = dab.Hue;
                    }
                    canvas.Bare[index] = bare * (1f - alpha);
                }
            }
        }

        // Smoothstep-interpolated hashed 1D lattice in [-1, 1].
        private static float ValueNoise1D(float x, int seed)
        {
            int xi = (int)Math.Floor(x);
            float f = x - xi;
            float va = Hash(xi, 0, seed) / 8388607.5f - 1f;
            float vb = Hash(xi + 1, 0, seed) / 8388607.5f - 1f;
            return va + (vb - va) * (f * f * (3f - 2f * f));
        }

        // A: low-frequency soft blotches (same construction as BrushNoiseBaker's blotch: periodic Perlin fbm, base
        // frequency 3, 4 octaves, periodic domain warp), with its own seed stream.
        private static float Blotch(int i, int j, int size, int seed)
        {
            const int baseFreq = 3;
            const int octaves = 4;
            float x0 = i * (float)baseFreq / size;
            float y0 = j * (float)baseFreq / size;

            float warpX = PeriodicNoise(x0, y0, baseFreq, baseFreq, seed ^ 0x1B873593);
            float warpY = PeriodicNoise(x0 + 19f, y0 + 7f, baseFreq, baseFreq, seed ^ 0x3C6EF372);
            const float warpAmount = 0.35f;
            float xk = x0 + warpAmount * warpX;
            float yk = y0 + warpAmount * warpY;

            float sum = 0f, norm = 0f, amplitude = 1f;
            int period = baseFreq;
            for (int o = 0; o < octaves; o++)
            {
                sum += PeriodicNoise(xk, yk, period, period, seed + 0x4000 + o * 131) * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                xk *= 2f; yk *= 2f; period *= 2;
            }
            return sum / norm;
        }

        // Perlin-style gradient noise on a lattice wrapped modulo (periodX, periodY): exactly periodic.
        private static float PeriodicNoise(float x, float y, int periodX, int periodY, int seed)
        {
            int xi = (int)Math.Floor(x);
            int yi = (int)Math.Floor(y);
            float xf = x - xi;
            float yf = y - yi;
            int x0 = Mod(xi, periodX);
            int x1 = Mod(xi + 1, periodX);
            int y0 = Mod(yi, periodY);
            int y1 = Mod(yi + 1, periodY);
            float u = Fade(xf);
            float v = Fade(yf);
            float n00 = Grad(Hash(x0, y0, seed), xf, yf);
            float n10 = Grad(Hash(x1, y0, seed), xf - 1f, yf);
            float n01 = Grad(Hash(x0, y1, seed), xf, yf - 1f);
            float n11 = Grad(Hash(x1, y1, seed), xf - 1f, yf - 1f);
            return Lerp(Lerp(n00, n10, u), Lerp(n01, n11, u), v);
        }

        private static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        private static float Lerp(float a, float b, float t) => a + t * (b - a);
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Grad(int hash, float x, float y)
        {
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        // 24-bit hash in [0, 16777215].
        private static int Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * -1640531527;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return h & 0xFFFFFF;
            }
        }

        // Rescales so the mean lands on mid grey and the largest deviation touches 0 or 255.
        private static void NormalizeCentredInto(float[] channel, byte[] bytes, int channelOffset)
        {
            double sum = 0;
            for (int p = 0; p < channel.Length; p++) sum += channel[p];
            float mean = (float)(sum / channel.Length);
            float maxDeviation = 1e-6f;
            for (int p = 0; p < channel.Length; p++)
                maxDeviation = Math.Max(maxDeviation, Math.Abs(channel[p] - mean));

            float scale = 0.5f / maxDeviation;
            for (int p = 0; p < channel.Length; p++)
            {
                float t = 0.5f + (channel[p] - mean) * scale;
                int value = (int)Math.Round(t * 255f, MidpointRounding.AwayFromZero);
                bytes[p * 4 + channelOffset] = (byte)Math.Min(255, Math.Max(0, value));
            }
        }

        // Rescales so the mean lands on mid grey with a fixed standard deviation (0..1 units), clamping the rare tails:
        // the contrast no longer depends on the channel's single most extreme pixel.
        private static void NormalizeStdDevInto(float[] channel, byte[] bytes, int channelOffset, float targetStdDev)
        {
            double sum = 0, sumSquares = 0;
            for (int p = 0; p < channel.Length; p++)
            {
                sum += channel[p];
                sumSquares += (double)channel[p] * channel[p];
            }
            double mean = sum / channel.Length;
            double variance = Math.Max(0.0, sumSquares / channel.Length - mean * mean);

            float scale = targetStdDev / (float)Math.Max(Math.Sqrt(variance), 1e-6);
            for (int p = 0; p < channel.Length; p++)
            {
                float t = 0.5f + (channel[p] - (float)mean) * scale;
                int value = (int)Math.Round(t * 255f, MidpointRounding.AwayFromZero);
                bytes[p * 4 + channelOffset] = (byte)Math.Min(255, Math.Max(0, value));
            }
        }

        // PCG32 (XSH-RR): small, fast, deterministic across runtimes.
        private struct Pcg32
        {
            private const ulong Multiplier = 6364136223846793005UL;
            private const ulong Increment = 1442695040888963407UL;
            private ulong _state;

            public Pcg32(int seed)
            {
                unchecked
                {
                    // SplitMix64 scramble so neighbouring seeds start far apart.
                    ulong z = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
                    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                    z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                    z ^= z >> 31;
                    _state = 0UL;
                    NextUInt();
                    _state += z;
                    NextUInt();
                }
            }

            public uint NextUInt()
            {
                unchecked
                {
                    ulong old = _state;
                    _state = old * Multiplier + Increment;
                    uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
                    int rotation = (int)(old >> 59);
                    return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
                }
            }

            /// <summary>Uniform in [0, 1).</summary>
            public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

            public int NextInt(int exclusiveMax) => (int)(NextUInt() % (uint)exclusiveMax);
        }
    }
}
