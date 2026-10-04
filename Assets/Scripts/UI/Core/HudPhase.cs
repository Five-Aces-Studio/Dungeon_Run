namespace DungeonRun.UI.Presentation
{
    /// <summary>Presentation mirror of DungeonRun.Combat.BattlePhase. Same order, so callers convert with (HudPhase)(int)phase.</summary>
    public enum HudPhase { Planning, EnemyReveal, Resolution, Cleanup, Refill, Victory, Defeat, Draw }
}
