using NUnit.Framework;

namespace DungeonRun.UI.Presentation.Tests
{
    public sealed class HudStatusFormatTests
    {
        [TestCase(1, HudPhase.Planning, "TURN 1  \u00B7  PLANNING")]
        [TestCase(3, HudPhase.EnemyReveal, "TURN 3  \u00B7  ENEMY REVEAL")]
        [TestCase(2, HudPhase.Resolution, "TURN 2  \u00B7  RESOLUTION")]
        [TestCase(2, HudPhase.Cleanup, "TURN 2  \u00B7  RESOLUTION")]
        [TestCase(2, HudPhase.Refill, "TURN 2  \u00B7  RESOLUTION")]
        [TestCase(7, HudPhase.Victory, "TURN 7  \u00B7  VICTORY")]
        [TestCase(4, HudPhase.Defeat, "TURN 4  \u00B7  DEFEAT")]
        [TestCase(5, HudPhase.Draw, "TURN 5  \u00B7  MUTUAL DEFEAT")]
        [TestCase(12, HudPhase.Planning, "TURN 12  \u00B7  PLANNING")]
        public void Status_FormatsTurnAndPhaseLabel(int turn, HudPhase phase, string expected) =>
            Assert.AreEqual(expected, HudStatusFormat.Status(turn, phase));

        [TestCase(0, HudPhase.Planning, "PLANNING")]
        [TestCase(-1, HudPhase.Victory, "VICTORY")]
        [TestCase(0, HudPhase.Draw, "MUTUAL DEFEAT")]
        public void Status_NoTurn_IsLabelOnly(int turn, HudPhase phase, string expected) =>
            Assert.AreEqual(expected, HudStatusFormat.Status(turn, phase));

        [Test]
        public void Status_EveryPhaseHasALabel()
        {
            foreach (HudPhase phase in System.Enum.GetValues(typeof(HudPhase)))
                Assert.IsNotEmpty(HudStatusFormat.PhaseLabel(phase), phase.ToString());
        }

        [Test]
        public void Status_UsesMiddleDotSeparator() =>
            StringAssert.Contains("\u00B7", HudStatusFormat.Status(1, HudPhase.Planning));

        [TestCase(1, "I")]
        [TestCase(2, "II")]
        [TestCase(3, "III")]
        [TestCase(4, "IV")]
        [TestCase(5, "V")]
        [TestCase(6, "VI")]
        [TestCase(7, "VII")]
        [TestCase(8, "VIII")]
        [TestCase(9, "IX")]
        [TestCase(10, "X")]
        public void Roman_OneToTen(int n, string expected) => Assert.AreEqual(expected, HudStatusFormat.Roman(n));

        [TestCase(0, "0")]
        [TestCase(-2, "-2")]
        [TestCase(11, "11")]
        [TestCase(40, "40")]
        public void Roman_OutOfRange_IsDecimal(int n, string expected) => Assert.AreEqual(expected, HudStatusFormat.Roman(n));
    }
}
