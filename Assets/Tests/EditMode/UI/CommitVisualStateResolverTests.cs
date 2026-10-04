using NUnit.Framework;

namespace DungeonRun.UI.Presentation.Tests
{
    public sealed class CommitVisualStateResolverTests
    {
        private static CommitVisual Call(
            bool isPreview = false,
            bool isTerminal = false,
            HudPhase phase = HudPhase.Planning,
            bool isResolving = false,
            bool canResolve = false,
            bool pointerOver = false,
            bool pointerDown = false) =>
            CommitVisualStateResolver.Resolve(isPreview, isTerminal, phase, isResolving, canResolve, pointerOver, pointerDown);

        private static void AssertVisual(CommitVisualState state, string label, CommitVisual actual)
        {
            Assert.AreEqual(state, actual.State);
            Assert.AreEqual(label, actual.Label);
        }

        [Test]
        public void Constructor_StoresStateAndLabel()
        {
            var visual = new CommitVisual(CommitVisualState.Hover, "COMMIT");
            AssertVisual(CommitVisualState.Hover, "COMMIT", visual);
        }

        [Test]
        public void Planning_CannotResolve_IsDisabledCommit() =>
            AssertVisual(CommitVisualState.Disabled, "COMMIT", Call());

        [Test]
        public void Planning_CannotResolve_IgnoresPointer() =>
            AssertVisual(CommitVisualState.Disabled, "COMMIT", Call(pointerOver: true, pointerDown: true));

        [Test]
        public void Planning_CanResolve_IsReadyCommit() =>
            AssertVisual(CommitVisualState.Ready, "COMMIT", Call(canResolve: true));

        [Test]
        public void Planning_CanResolve_PointerOver_IsHover() =>
            AssertVisual(CommitVisualState.Hover, "COMMIT", Call(canResolve: true, pointerOver: true));

        [Test]
        public void Planning_CanResolve_PointerDown_IsPressed() =>
            AssertVisual(CommitVisualState.Pressed, "COMMIT", Call(canResolve: true, pointerDown: true));

        [Test]
        public void Planning_PointerDownBeatsPointerOver() =>
            AssertVisual(CommitVisualState.Pressed, "COMMIT", Call(canResolve: true, pointerOver: true, pointerDown: true));

        [Test]
        public void Preview_PlanningLabels_AreResolve()
        {
            AssertVisual(CommitVisualState.Disabled, "RESOLVE", Call(isPreview: true));
            AssertVisual(CommitVisualState.Ready, "RESOLVE", Call(isPreview: true, canResolve: true));
            AssertVisual(CommitVisualState.Hover, "RESOLVE", Call(isPreview: true, canResolve: true, pointerOver: true));
            AssertVisual(CommitVisualState.Pressed, "RESOLVE", Call(isPreview: true, canResolve: true, pointerDown: true));
        }

        [Test]
        public void EnemyReveal_IsLocked()
        {
            AssertVisual(CommitVisualState.Locked, "LOCKED", Call(phase: HudPhase.EnemyReveal));
            AssertVisual(CommitVisualState.Locked, "LOCKED",
                Call(isPreview: true, phase: HudPhase.EnemyReveal, isResolving: true, canResolve: true,
                    pointerOver: true, pointerDown: true));
        }

        [TestCase(HudPhase.Resolution)]
        [TestCase(HudPhase.Cleanup)]
        [TestCase(HudPhase.Refill)]
        public void ResolvingPhases_AreResolving(HudPhase phase)
        {
            AssertVisual(CommitVisualState.Resolving, "RESOLVING", Call(phase: phase));
            AssertVisual(CommitVisualState.Resolving, "RESOLVING",
                Call(isPreview: true, phase: phase, isResolving: true, canResolve: true, pointerOver: true,
                    pointerDown: true));
        }

        [Test]
        public void Planning_IsResolving_IsResolving()
        {
            AssertVisual(CommitVisualState.Resolving, "RESOLVING", Call(isResolving: true));
            AssertVisual(CommitVisualState.Resolving, "RESOLVING",
                Call(isPreview: true, isResolving: true, canResolve: true, pointerOver: true, pointerDown: true));
        }

        [TestCase(HudPhase.Planning)]
        [TestCase(HudPhase.EnemyReveal)]
        [TestCase(HudPhase.Resolution)]
        [TestCase(HudPhase.Cleanup)]
        [TestCase(HudPhase.Refill)]
        [TestCase(HudPhase.Victory)]
        [TestCase(HudPhase.Defeat)]
        [TestCase(HudPhase.Draw)]
        public void Terminal_BeatsEverything(HudPhase phase)
        {
            AssertVisual(CommitVisualState.Complete, "COMPLETE", Call(isTerminal: true, phase: phase));
            AssertVisual(CommitVisualState.Complete, "COMPLETE",
                Call(isPreview: true, isTerminal: true, phase: phase, isResolving: true, canResolve: true,
                    pointerOver: true, pointerDown: true));
        }
    }
}
