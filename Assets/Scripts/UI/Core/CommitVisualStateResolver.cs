namespace DungeonRun.UI.Presentation
{
    /// <summary>Pure rule set: commit inputs and battle phase -&gt; plate state and label.</summary>
    public static class CommitVisualStateResolver
    {
        public const string CommitLabel = "COMMIT", PreviewLabel = "RESOLVE", LockedLabel = "LOCKED",
            ResolvingLabel = "RESOLVING", CompleteLabel = "COMPLETE";

        /// <remarks>Precedence: terminal &gt; reveal lock &gt; resolving &gt; planning (pressed &gt; hover &gt; ready, or disabled).</remarks>
        public static CommitVisual Resolve(bool isPreview, bool isTerminal, HudPhase phase, bool isResolving, bool canResolve,
            bool pointerOver, bool pointerDown)
        {
            if (isTerminal) return new CommitVisual(CommitVisualState.Complete, CompleteLabel);
            if (phase == HudPhase.EnemyReveal) return new CommitVisual(CommitVisualState.Locked, LockedLabel);
            if (HudPhases.IsResolving(phase) || isResolving && phase == HudPhase.Planning)
                return new CommitVisual(CommitVisualState.Resolving, ResolvingLabel);
            var label = isPreview ? PreviewLabel : CommitLabel;
            if (!canResolve) return new CommitVisual(CommitVisualState.Disabled, label);
            var state = pointerDown ? CommitVisualState.Pressed : pointerOver ? CommitVisualState.Hover : CommitVisualState.Ready;
            return new CommitVisual(state, label);
        }
    }
}
