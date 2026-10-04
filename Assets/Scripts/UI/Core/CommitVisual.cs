namespace DungeonRun.UI.Presentation
{
    /// <summary>Resolved Commit button presentation: visual state plus label text.</summary>
    public readonly struct CommitVisual
    {
        public CommitVisualState State { get; }
        public string Label { get; }
        public CommitVisual(CommitVisualState state, string label) { State = state; Label = label; }
    }
}
