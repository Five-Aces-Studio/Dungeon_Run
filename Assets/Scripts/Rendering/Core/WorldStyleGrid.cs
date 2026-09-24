using System;

namespace DungeonRun.Rendering
{
    /// <summary>Maps a 1080p-reference pixel scale to an integer device-pixel cell size for any output height.</summary>
    public static class WorldStyleGrid
    {
        public static int CellPixels(float pixelScale, int outputHeight)
        {
            if (outputHeight <= 0 || float.IsNaN(pixelScale) || float.IsInfinity(pixelScale) || pixelScale <= 0f)
                return 1;
            int cells = (int)Math.Floor(pixelScale * outputHeight / 1080.0 + 0.5);
            return Math.Max(1, cells);
        }
    }
}
