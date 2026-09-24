using System;

namespace DungeonRun.Rendering
{
    /// <summary>
    /// Deterministic flame flicker: three layers of smooth 1D value noise at incommensurate frequencies plus a slow
    /// sine sway. Allocation-free and stateless, so the light, the painted glow pool and the flame card shader can
    /// all derive the same signal from (time, seed).
    /// </summary>
    public static class FlameFlicker
    {
        // Lattice frequencies in Hz and their amplitudes: a slow breath, a mid flutter and a fast crackle.
        private const double Frequency0 = 2.1, Amplitude0 = 0.55;
        private const double Frequency1 = 5.3, Amplitude1 = 0.30;
        private const double Frequency2 = 11.7, Amplitude2 = 0.15;
        private const double SwayFrequency = 0.37, SwayAmplitude = 0.15;
        private const double AmplitudeSum = Amplitude0 + Amplitude1 + Amplitude2 + SwayAmplitude;

        // Summed interpolated noise rarely reaches the full amplitude sum; a mild gain spends more of [-1, 1]
        // and the final clamp only trims the rare peaks.
        private const double Gain = 1.35;

        // Seed-dependent phase shift, in lattice cells, so seeds decorrelate even where their hashes line up.
        private const double PhaseRange = 64.0;

        /// <summary>Deterministic, continuous flame flicker signal in [-1, 1]. Non-finite time returns 0.</summary>
        public static float Evaluate(float time, int seed)
        {
            if (float.IsNaN(time) || float.IsInfinity(time)) return 0f;

            double t = time;
            double sum =
                Amplitude0 * ValueNoise(t * Frequency0 + Phase(seed, 0), seed, 0) +
                Amplitude1 * ValueNoise(t * Frequency1 + Phase(seed, 1), seed, 1) +
                Amplitude2 * ValueNoise(t * Frequency2 + Phase(seed, 2), seed, 2) +
                SwayAmplitude * Math.Sin(2.0 * Math.PI * (t * SwayFrequency + Phase(seed, 3) / PhaseRange));

            double value = sum / AmplitudeSum * Gain;
            return (float)(value < -1.0 ? -1.0 : (value > 1.0 ? 1.0 : value));
        }

        // Smoothstep-interpolated hashed lattice in [-1, 1]: C1-continuous, bounded by its lattice values.
        private static double ValueNoise(double x, int seed, int layer)
        {
            double cell = Math.Floor(x);
            double f = x - cell;
            long index = (long)cell;
            double a = Lattice(index, seed, layer);
            double b = Lattice(index + 1, seed, layer);
            double u = f * f * (3.0 - 2.0 * f);
            return a + (b - a) * u;
        }

        private static double Lattice(long index, int seed, int layer)
        {
            int folded = unchecked((int)index ^ (int)(index >> 32));
            return Hash(folded, seed, layer) / 8388607.5 - 1.0;
        }

        private static double Phase(int seed, int layer) => Hash(-1 - layer, seed, layer + 17) / 16777215.0 * PhaseRange;

        // 24-bit hash in [0, 16777215].
        private static int Hash(int x, int seed, int layer)
        {
            unchecked
            {
                int h = x * 374761393 + seed * 668265263 + layer * -1640531527;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return h & 0xFFFFFF;
            }
        }
    }
}
