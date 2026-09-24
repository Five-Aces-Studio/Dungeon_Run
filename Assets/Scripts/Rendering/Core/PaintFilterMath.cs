using System;

namespace DungeonRun.Rendering
{
    /// <summary>Resolution mapping for the screen-space acrylic paint filter (anisotropic Kuwahara on a half-res buffer).</summary>
    public static class PaintFilterMath
    {
        public const float MaxRadius = 8f;

        /// <summary>
        /// Screen-space Kuwahara radius in pixels of the (half-resolution) paint buffer.
        /// radius1080 = 1080p-reference full-res pixels. Invalid height or non-finite radius -> 1.
        /// Result clamped to [1, MaxRadius].
        /// </summary>
        public static float KuwaharaRadius(float radius1080, int outputHeight, bool halfResolution)
        {
            if (outputHeight <= 0 || float.IsNaN(radius1080) || float.IsInfinity(radius1080))
                return 1f;
            float radius = radius1080 * outputHeight / 1080f * (halfResolution ? 0.5f : 1f);
            return Math.Min(MaxRadius, Math.Max(1f, radius));
        }

        /// <summary>Half-resolution size: max(1, (n + 1) / 2) per axis, without overflow near int.MaxValue.</summary>
        public static int HalfSize(int fullSize) => Math.Max(1, fullSize / 2 + (fullSize & 1));
    }
}
