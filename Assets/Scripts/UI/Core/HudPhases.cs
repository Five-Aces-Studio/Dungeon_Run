namespace DungeonRun.UI.Presentation
{
    /// <summary>Shared phase groupings used by the presentation resolvers.</summary>
    internal static class HudPhases
    {
        public static bool IsTerminal(HudPhase phase) =>
            phase == HudPhase.Victory || phase == HudPhase.Defeat || phase == HudPhase.Draw;

        public static bool IsResolving(HudPhase phase) =>
            phase == HudPhase.Resolution || phase == HudPhase.Cleanup || phase == HudPhase.Refill;
    }
}
