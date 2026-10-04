using NUnit.Framework;

namespace DungeonRun.UI.Presentation.Tests
{
    public sealed class ActionPipLayoutTests
    {
        private const PipState A = PipState.Available;
        private const PipState S = PipState.Spent;

        [TestCase(2, 2, "2 / 2")]
        [TestCase(1, 2, "1 / 2")]
        [TestCase(0, 2, "0 / 2")]
        [TestCase(3, 4, "3 / 4")]
        [TestCase(0, 0, "0 / 0")]
        public void Counter_FormatsActionsOverMax(int actions, int max, string expected) =>
            Assert.AreEqual(expected, ActionPipLayout.Counter(actions, max));

        [TestCase(5, 2, "2 / 2")]
        [TestCase(-3, 2, "0 / 2")]
        [TestCase(1, 0, "0 / 0")]
        public void Counter_ClampsActionsToRange(int actions, int max, string expected) =>
            Assert.AreEqual(expected, ActionPipLayout.Counter(actions, max));

        [TestCase(0, -1, "0 / 0")]
        [TestCase(2, -5, "0 / 0")]
        public void Counter_NegativeMax_IsZero(int actions, int max, string expected) =>
            Assert.AreEqual(expected, ActionPipLayout.Counter(actions, max));

        [Test]
        public void Pips_FullActions_AllAvailable() =>
            CollectionAssert.AreEqual(new[] { A, A }, ActionPipLayout.Pips(2, 2));

        [Test]
        public void Pips_PartialActions_AvailableThenSpent() =>
            CollectionAssert.AreEqual(new[] { A, S, S, S }, ActionPipLayout.Pips(1, 4));

        [Test]
        public void Pips_NoActions_AllSpent() =>
            CollectionAssert.AreEqual(new[] { S, S }, ActionPipLayout.Pips(0, 2));

        [Test]
        public void Pips_ActionsAboveMax_ClampedToMax() =>
            CollectionAssert.AreEqual(new[] { A, A, A }, ActionPipLayout.Pips(9, 3));

        [Test]
        public void Pips_NegativeActions_ClampedToZero() =>
            CollectionAssert.AreEqual(new[] { S, S, S }, ActionPipLayout.Pips(-2, 3));

        [Test]
        public void Pips_LengthEqualsMax_UpToDefaultVisible()
        {
            for (var max = 1; max <= 6; max++) Assert.AreEqual(max, ActionPipLayout.Pips(0, max).Length);
            CollectionAssert.AreEqual(new[] { A, A, A, A, A, S }, ActionPipLayout.Pips(5, 6));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Pips_NoMax_IsEmpty(int max) => Assert.IsEmpty(ActionPipLayout.Pips(2, max));

        [Test]
        public void Pips_AboveDefaultVisible_IsHidden() => Assert.IsEmpty(ActionPipLayout.Pips(3, 7));

        [Test]
        public void Pips_CustomMaxVisible_HidesAboveAndShowsAtLimit()
        {
            Assert.IsEmpty(ActionPipLayout.Pips(1, 4, maxVisible: 3));
            CollectionAssert.AreEqual(new[] { A, S, S }, ActionPipLayout.Pips(1, 3, maxVisible: 3));
            CollectionAssert.AreEqual(new[] { A, A, A, A, A, A, A, S }, ActionPipLayout.Pips(7, 8, maxVisible: 8));
        }

        [Test]
        public void Pips_NeverReturnsNull()
        {
            Assert.IsNotNull(ActionPipLayout.Pips(0, 0));
            Assert.IsNotNull(ActionPipLayout.Pips(0, 99));
        }

        [Test]
        public void Pips_ReturnsFreshArrayPerCall()
        {
            var first = ActionPipLayout.Pips(1, 2);
            first[1] = PipState.Hidden;
            CollectionAssert.AreEqual(new[] { A, S }, ActionPipLayout.Pips(1, 2));
        }

        [Test]
        public void PipState_OrderIsStable() =>
            CollectionAssert.AreEqual(new[] { "Hidden", "Spent", "Available" }, System.Enum.GetNames(typeof(PipState)));
    }
}
