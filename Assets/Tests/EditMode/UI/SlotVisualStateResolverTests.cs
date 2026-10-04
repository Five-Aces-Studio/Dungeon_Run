using NUnit.Framework;

namespace DungeonRun.UI.Presentation.Tests
{
    public sealed class SlotVisualStateResolverTests
    {
        private static SlotVisualState Call(
            bool occupied = false,
            bool cardArmed = false,
            bool dropOver = false,
            bool dropValid = false,
            HudPhase phase = HudPhase.Planning,
            bool isResolving = false,
            bool isTerminal = false) =>
            SlotVisualStateResolver.Resolve(occupied, cardArmed, dropOver, dropValid, phase, isResolving, isTerminal);

        [Test]
        public void Planning_NothingSet_IsEmpty() => Assert.AreEqual(SlotVisualState.Empty, Call());

        [Test]
        public void Planning_CardArmed_IsArmed() => Assert.AreEqual(SlotVisualState.Armed, Call(cardArmed: true));

        [Test]
        public void Planning_Occupied_IsQueued() => Assert.AreEqual(SlotVisualState.Queued, Call(occupied: true));

        [Test]
        public void Planning_OccupiedBeatsArmed() =>
            Assert.AreEqual(SlotVisualState.Queued, Call(occupied: true, cardArmed: true));

        [Test]
        public void Planning_DropOverValid_IsHoverValid() =>
            Assert.AreEqual(SlotVisualState.HoverValid, Call(dropOver: true, dropValid: true));

        [Test]
        public void Planning_DropOverInvalid_IsHoverInvalid() =>
            Assert.AreEqual(SlotVisualState.HoverInvalid, Call(dropOver: true, dropValid: false));

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void Planning_DropOverBeatsOccupiedAndArmed(bool occupied, bool cardArmed)
        {
            Assert.AreEqual(SlotVisualState.HoverValid, Call(occupied, cardArmed, dropOver: true, dropValid: true));
            Assert.AreEqual(SlotVisualState.HoverInvalid, Call(occupied, cardArmed, dropOver: true, dropValid: false));
        }

        [Test]
        public void Planning_DropValidWithoutDropOver_IsIgnored()
        {
            Assert.AreEqual(SlotVisualState.Empty, Call(dropValid: true));
            Assert.AreEqual(SlotVisualState.Armed, Call(cardArmed: true, dropValid: true));
            Assert.AreEqual(SlotVisualState.Queued, Call(occupied: true, dropValid: true));
        }

        [Test]
        public void Planning_IsResolving_IsResolving() =>
            Assert.AreEqual(SlotVisualState.Resolving, Call(isResolving: true));

        [Test]
        public void Planning_IsResolving_BeatsDropOverOccupiedAndArmed() =>
            Assert.AreEqual(SlotVisualState.Resolving,
                Call(occupied: true, cardArmed: true, dropOver: true, dropValid: true, isResolving: true));

        [Test]
        public void EnemyReveal_IsLocked()
        {
            Assert.AreEqual(SlotVisualState.Locked, Call(phase: HudPhase.EnemyReveal));
            Assert.AreEqual(SlotVisualState.Locked, Call(phase: HudPhase.EnemyReveal, isResolving: true));
            Assert.AreEqual(SlotVisualState.Locked,
                Call(occupied: true, cardArmed: true, dropOver: true, dropValid: true, phase: HudPhase.EnemyReveal));
        }

        [TestCase(HudPhase.Resolution)]
        [TestCase(HudPhase.Cleanup)]
        [TestCase(HudPhase.Refill)]
        public void ResolvingPhases_AreResolving(HudPhase phase)
        {
            Assert.AreEqual(SlotVisualState.Resolving, Call(phase: phase));
            Assert.AreEqual(SlotVisualState.Resolving, Call(phase: phase, isResolving: true));
            Assert.AreEqual(SlotVisualState.Resolving,
                Call(occupied: true, cardArmed: true, dropOver: true, dropValid: true, phase: phase));
        }

        [TestCase(HudPhase.Victory)]
        [TestCase(HudPhase.Defeat)]
        [TestCase(HudPhase.Draw)]
        public void TerminalPhases_AreLocked(HudPhase phase)
        {
            Assert.AreEqual(SlotVisualState.Locked, Call(phase: phase));
            Assert.AreEqual(SlotVisualState.Locked,
                Call(occupied: true, cardArmed: true, dropOver: true, dropValid: true, phase: phase, isResolving: true));
        }

        [TestCase(HudPhase.Planning)]
        [TestCase(HudPhase.EnemyReveal)]
        [TestCase(HudPhase.Resolution)]
        [TestCase(HudPhase.Cleanup)]
        [TestCase(HudPhase.Refill)]
        public void TerminalFlag_BeatsEverything(HudPhase phase)
        {
            Assert.AreEqual(SlotVisualState.Locked, Call(phase: phase, isTerminal: true));
            Assert.AreEqual(SlotVisualState.Locked,
                Call(occupied: true, cardArmed: true, dropOver: true, dropValid: true, phase: phase,
                    isResolving: true, isTerminal: true));
        }
    }
}
