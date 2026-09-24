using System;

namespace DungeonRun.Rendering
{
    /// <summary>Deterministic, exactly-tileable RGBA8 brush noise, baked on the CPU with no engine dependency.</summary>
    public static class BrushNoiseBaker
    {
        public static byte[] BakeRgba32(int size, int seed)
        {
            if (size < 16 || (size & (size - 1)) != 0)
                throw new ArgumentException("size must be a power of two and at least 16.", nameof(size));

            var r = new float[size, size];
            var g = new float[size, size];
            var b = new float[size, size];
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    r[i, j] = Blotch(i, j, size, seed);
                    g[i, j] = Stroke(i, j, size, seed);
                    b[i, j] = Mottle(i, j, size, seed);
                }
            }

            var bytes = new byte[size * size * 4];
            NormalizeInto(r, size, bytes, 0);
            NormalizeInto(g, size, bytes, 1);
            NormalizeInto(b, size, bytes, 2);
            for (int p = 3; p < bytes.Length; p += 4) bytes[p] = 255;
            return bytes;
        }

        // R: low-frequency soft blotches. fbm base freq ~3, 4 octaves, periodic domain warp.
        private static float Blotch(int i, int j, int size, int seed)
        {
            const int baseFreq = 3;
            const int octaves = 4;
            float x0 = i * (float)baseFreq / size;
            float y0 = j * (float)baseFreq / size;

            // Domain warp is itself periodic: it is a periodic function of already-periodic (x0, y0).
            float warpX = PeriodicNoise(x0, y0, baseFreq, baseFreq, seed ^ 0x51ED270B);
            float warpY = PeriodicNoise(x0 + 19f, y0 + 7f, baseFreq, baseFreq, seed ^ 0x2E9B5C2D);
            const float warpAmount = 0.35f;
            float xw = x0 + warpAmount * warpX;
            float yw = y0 + warpAmount * warpY;

            float sum = 0f, norm = 0f, amplitude = 1f;
            float xk = xw, yk = yw;
            int period = baseFreq;
            for (int o = 0; o < octaves; o++)
            {
                sum += PeriodicNoise(xk, yk, period, period, seed + o * 101) * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                xk *= 2f; yk *= 2f; period *= 2;
            }
            return sum / norm;
        }

        // G: irregular brush strokes. Two anisotropic 2D stroke fields on the integer lattice directions (2,1)
        // and (1,2), each curved by a periodic domain warp and blended by a low-frequency region mask so stroke
        // direction varies across the tile. Every coordinate shift under a tile wrap is an integer multiple of the
        // matching noise period, so the result stays exactly periodic.
        private static float Stroke(int i, int j, int size, int seed)
        {
            float x = (float)i / size, y = (float)j / size;
            float warpA = PeriodicNoise(x * 3f, y * 3f, 3, 3, seed ^ 0x6A09E667);
            float warpB = PeriodicNoise(x * 3f + 11f, y * 3f + 5f, 3, 3, seed ^ 0x3C6EF372);
            const float warp = 0.3f;
            float fieldA = StrokeField(2f * x + y + warp * warpA, -x + 2f * y + warp * warpB, seed ^ 0x1000);
            float fieldB = StrokeField(x + 2f * y + warp * warpB, -2f * x + y + warp * warpA, seed ^ 0x2000);
            float region = PeriodicNoise(x * 2f, y * 2f, 2, 2, seed ^ 0x7F4A7C15);
            float t = SmoothStep(-0.15f, 0.15f, region);
            return Lerp(fieldA, fieldB, t);
        }

        // Long along the stroke, short across it (2:6 cells per lattice unit), plus one half-size octave.
        private static float StrokeField(float along, float across, int seed)
        {
            float sum = PeriodicNoise(along * 2f, across * 6f, 2, 6, seed) +
                0.5f * PeriodicNoise(along * 4f, across * 12f, 4, 12, seed + 17);
            return sum / 1.5f;
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Math.Min(1f, Math.Max(0f, (x - edge0) / (edge1 - edge0)));
            return t * t * (3f - 2f * t);
        }

        // B: mid-frequency mottling. fbm freq ~8, 3 octaves.
        private static float Mottle(int i, int j, int size, int seed)
        {
            const int baseFreq = 8;
            const int octaves = 3;
            float x0 = i * (float)baseFreq / size;
            float y0 = j * (float)baseFreq / size;

            float sum = 0f, norm = 0f, amplitude = 1f;
            float xk = x0, yk = y0;
            int period = baseFreq;
            for (int o = 0; o < octaves; o++)
            {
                sum += PeriodicNoise(xk, yk, period, period, seed ^ (0x3000 + o * 97)) * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                xk *= 2f; yk *= 2f; period *= 2;
            }
            return sum / norm;
        }

        /// <summary>
        /// Perlin-style gradient noise, hashed on a lattice wrapped modulo (periodX, periodY). Exactly periodic:
        /// PeriodicNoise(x + k*periodX, y, periodX, periodY, seed) == PeriodicNoise(x, y, periodX, periodY, seed)
        /// for any integer k, and likewise in y, because only (floor(coord) mod period) and the fractional part
        /// feed the result, and both repeat exactly under such a shift.
        /// </summary>
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

        private static int Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * -1640531527;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return h;
            }
        }

        private static void NormalizeInto(float[,] channel, int size, byte[] bytes, int channelOffset)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float v = channel[i, j];
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }
            float range = Math.Max(max - min, 1e-6f);
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float t = (channel[i, j] - min) / range;
                    int idx = (j * size + i) * 4 + channelOffset;
                    int value = (int)Math.Round(t * 255f, MidpointRounding.AwayFromZero);
                    bytes[idx] = (byte)Math.Min(255, Math.Max(0, value));
                }
            }
        }
    }
}
