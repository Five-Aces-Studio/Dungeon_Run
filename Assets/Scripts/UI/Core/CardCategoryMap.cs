namespace DungeonRun.UI.Presentation
{
    /// <summary>PreviewCardKind ordinal -&gt; card category, footer label and glyph index.</summary>
    /// <remarks>Kind order: Attack=0, Defence=1, Dodge=2, Heal=3, Piercing=4, Combo=5, Counterattack=6, Charge=7, Miss=8.</remarks>
    public static class CardCategoryMap
    {
        public const int MissKind = 8;

        private static readonly CardCategory[] Categories =
        {
            CardCategory.Attack, CardCategory.Defence, CardCategory.Mobility, CardCategory.Support, CardCategory.Attack,
            CardCategory.Attack, CardCategory.Mobility, CardCategory.Special, CardCategory.Special
        };

        private static readonly string[] Labels = { "ATTACK", "DEFENCE", "MOBILITY", "SUPPORT", "SPECIAL" };

        /// <summary>Out-of-range kinds fall back to Attack, matching CombatHUDTheme.Icon.</summary>
        public static CardCategory Category(int previewCardKind) =>
            previewCardKind >= 0 && previewCardKind < Categories.Length ? Categories[previewCardKind] : CardCategory.Attack;

        public static string FooterLabel(int previewCardKind) =>
            previewCardKind == MissKind ? "NO EFFECT" : Labels[(int)Category(previewCardKind)];

        public static int GlyphIndex(int previewCardKind) => (int)Category(previewCardKind);
    }
}
