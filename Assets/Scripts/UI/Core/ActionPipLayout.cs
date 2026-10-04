using System;

namespace DungeonRun.UI.Presentation
{
    /// <summary>Action medallion counter text and pip row layout.</summary>
    public static class ActionPipLayout
    {
        public const int DefaultMaxVisible = 6;

        public static string Counter(int actions, int maxActions)
        {
            var max = Math.Max(0, maxActions);
            return Clamp(actions, max) + " / " + max;
        }

        /// <summary>Empty when there is no maximum or it exceeds <paramref name="maxVisible"/>; otherwise available pips first.</summary>
        public static PipState[] Pips(int actions, int maxActions, int maxVisible = DefaultMaxVisible)
        {
            if (maxActions <= 0 || maxActions > maxVisible) return Array.Empty<PipState>();
            var pips = new PipState[maxActions];
            var available = Clamp(actions, maxActions);
            for (var i = 0; i < pips.Length; i++) pips[i] = i < available ? PipState.Available : PipState.Spent;
            return pips;
        }

        private static int Clamp(int actions, int max) => Math.Min(Math.Max(0, actions), max);
    }
}
