using System;

namespace DungeonRun.UI.Presentation
{
    /// <summary>Icon shown beside a floating feedback label. Damage..Defeat index the style's feedback glyph array.</summary>
    public enum FeedbackGlyph { None = -1, Damage, Pierce, Block, Dodge, Heal, Charge, Defeat }

    /// <summary>Maps the combat feedback strings ("-N", "PIERCED N", "BLOCK N", "DODGE", "+N", "CHARGED", "DEFEATED") to V4 presentation.</summary>
    public static class HudFeedbackFormat
    {
        public static FeedbackGlyph Glyph(string message)
        {
            if (string.IsNullOrEmpty(message)) return FeedbackGlyph.None;
            if (message[0] == '-') return FeedbackGlyph.Damage;
            if (message[0] == '+') return FeedbackGlyph.Heal;
            if (message.StartsWith("PIERCED", StringComparison.Ordinal)) return FeedbackGlyph.Pierce;
            if (message.StartsWith("BLOCK", StringComparison.Ordinal)) return FeedbackGlyph.Block;
            if (message.StartsWith("DODGE", StringComparison.Ordinal)) return FeedbackGlyph.Dodge;
            if (message.StartsWith("CHARGED", StringComparison.Ordinal)) return FeedbackGlyph.Charge;
            if (message.StartsWith("DEFEATED", StringComparison.Ordinal)) return FeedbackGlyph.Defeat;
            return FeedbackGlyph.None;
        }

        /// <summary>Signed amounts ("-1", "+2") use the large number size; words use the small size.</summary>
        public static bool IsNumber(string message) =>
            !string.IsNullOrEmpty(message) && (message[0] == '-' || message[0] == '+');
    }
}
