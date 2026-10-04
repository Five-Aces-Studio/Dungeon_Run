using System;
using NUnit.Framework;

namespace DungeonRun.UI.Presentation.Tests
{
    public sealed class HudPhaseTests
    {
        // Mirrors DungeonRun.Combat.BattlePhase; callers convert with (HudPhase)(int)phase.
        private static readonly string[] BattlePhaseOrder =
            { "Planning", "EnemyReveal", "Resolution", "Cleanup", "Refill", "Victory", "Defeat", "Draw" };

        [Test]
        public void Names_MatchBattlePhaseOrder()
        {
            CollectionAssert.AreEqual(BattlePhaseOrder, Enum.GetNames(typeof(HudPhase)));
        }

        [Test]
        public void Values_AreSequentialFromZero()
        {
            for (var i = 0; i < BattlePhaseOrder.Length; i++)
                Assert.AreEqual(BattlePhaseOrder[i], ((HudPhase)i).ToString());
            Assert.AreEqual(BattlePhaseOrder.Length, Enum.GetValues(typeof(HudPhase)).Length);
        }
    }
}
