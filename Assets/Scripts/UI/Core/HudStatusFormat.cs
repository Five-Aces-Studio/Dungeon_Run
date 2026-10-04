namespace DungeonRun.UI.Presentation
{
    /// <summary>V4 status line and rune numeral formatting.</summary>
    public static class HudStatusFormat
    {
        public const string Separator = "  \u00B7  ";

        private static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

        /// <summary>"TURN {turn}  \u00B7  {label}"; just the label when there is no turn yet.</summary>
        public static string Status(int turn, HudPhase phase) =>
            turn <= 0 ? PhaseLabel(phase) : "TURN " + turn + Separator + PhaseLabel(phase);

        public static string PhaseLabel(HudPhase phase)
        {
            switch (phase)
            {
                case HudPhase.Planning: return "PLANNING";
                case HudPhase.EnemyReveal: return "ENEMY REVEAL";
                case HudPhase.Victory: return "VICTORY";
                case HudPhase.Defeat: return "DEFEAT";
                case HudPhase.Draw: return "MUTUAL DEFEAT";
                default: return "RESOLUTION";
            }
        }

        /// <summary>Roman numeral for 1..10; any other value as a decimal string.</summary>
        public static string Roman(int n) => n >= 1 && n <= Numerals.Length ? Numerals[n - 1] : n.ToString();
    }
}
