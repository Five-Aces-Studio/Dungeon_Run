namespace DungeonRun.UI.Presentation
{
    /// <summary>Action slot row positions and scale: base (1-2 slots) or dense (3+ slots) layout.</summary>
    public static class SlotRowLayout
    {
        public readonly struct Result
        {
            public readonly float[] Positions;
            public readonly float Scale;
            public Result(float[] positions, float scale) { Positions = positions; Scale = scale; }
        }

        /// <summary>Even row centred on <paramref name="baseCenterX"/>/<paramref name="denseCenterX"/>; dense kicks in at <paramref name="denseFromCount"/> or more slots.</summary>
        public static Result Layout(int count, float baseCenterX, float baseSpacing, float denseCenterX, float denseSpacing, float denseScale, int denseFromCount)
        {
            bool dense = count >= denseFromCount;
            float centerX = dense ? denseCenterX : baseCenterX;
            float spacing = dense ? denseSpacing : baseSpacing;
            var positions = new float[count];
            for (int i = 0; i < count; i++) positions[i] = centerX + (i - (count - 1) * .5f) * spacing;
            return new Result(positions, dense ? denseScale : 1f);
        }
    }
}
