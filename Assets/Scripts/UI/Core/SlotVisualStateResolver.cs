namespace DungeonRun.UI.Presentation
{
    /// <summary>Pure rule set: slot inputs and battle phase -&gt; socket visual state.</summary>
    public static class SlotVisualStateResolver
    {
        /// <remarks>Precedence: terminal &gt; reveal lock &gt; resolving &gt; drop hover &gt; queued &gt; armed &gt; empty.</remarks>
        public static SlotVisualState Resolve(bool occupied, bool cardArmed, bool dropOver, bool dropValid, HudPhase phase,
            bool isResolving, bool isTerminal)
        {
            if (isTerminal || HudPhases.IsTerminal(phase) || phase == HudPhase.EnemyReveal) return SlotVisualState.Locked;
            if (HudPhases.IsResolving(phase) || isResolving) return SlotVisualState.Resolving;
            if (dropOver) return dropValid ? SlotVisualState.HoverValid : SlotVisualState.HoverInvalid;
            if (occupied) return SlotVisualState.Queued;
            return cardArmed ? SlotVisualState.Armed : SlotVisualState.Empty;
        }
    }
}
