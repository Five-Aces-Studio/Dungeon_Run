namespace DungeonRun.UI.Presentation
{
    /// <summary>Presentation-only uniform size of an enemy's visual root (never a gameplay value).</summary>
    public static class EnemyVisualScaleRules
    {
        public const float Min = .5f, Max = 3f;

        public static float Resolve(float requested)
        {
            if (float.IsNaN(requested) || float.IsInfinity(requested)) return 1f;
            return requested < Min ? Min : requested > Max ? Max : requested;
        }

        /// <summary>Scale that makes a model of <paramref name="baseHeight"/> stand <paramref name="ratio"/> times the reference height.</summary>
        public static float ForHeightRatio(float ratio, float referenceHeight, float baseHeight)
        {
            if (!(ratio > 0f) || !(referenceHeight > 0f) || !(baseHeight > 0f)) return 1f;
            return Resolve(ratio * referenceHeight / baseHeight);
        }
    }
}
