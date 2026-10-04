using NUnit.Framework;

namespace DungeonRun.UI.Presentation.Tests
{
    public sealed class SlotRowLayoutTests
    {
        [Test]
        public void Layout_BelowDenseThreshold_UsesBaseCenterAndSpacing()
        {
            var result = SlotRowLayout.Layout(2, -127.5f, 150f, -95f, 112f, .82f, 3);
            Assert.AreEqual(new[] { -202.5f, -52.5f }, result.Positions);
            Assert.AreEqual(1f, result.Scale);
        }

        [Test]
        public void Layout_OneSlot_SitsAtBaseCenter()
        {
            var result = SlotRowLayout.Layout(1, -127.5f, 150f, -95f, 112f, .82f, 3);
            Assert.AreEqual(new[] { -127.5f }, result.Positions);
            Assert.AreEqual(1f, result.Scale);
        }

        [Test]
        public void Layout_AtDenseThreshold_UsesDenseCenterSpacingAndScale()
        {
            var result = SlotRowLayout.Layout(3, -127.5f, 150f, -95f, 112f, .82f, 3);
            Assert.AreEqual(new[] { -207f, -95f, 17f }, result.Positions);
            Assert.AreEqual(.82f, result.Scale);
        }

        [Test]
        public void Layout_FourSlots_IsDenseAndSymmetric()
        {
            var result = SlotRowLayout.Layout(4, -127.5f, 150f, -95f, 112f, .82f, 3);
            Assert.AreEqual(4, result.Positions.Length);
            Assert.AreEqual(.82f, result.Scale);
            // Symmetric around the dense centre.
            float centre = (result.Positions[0] + result.Positions[3]) * .5f;
            Assert.AreEqual(-95f, centre, 1e-4f);
            Assert.AreEqual((result.Positions[1] + result.Positions[2]) * .5f, centre, 1e-4f);
            Assert.AreEqual(112f, result.Positions[1] - result.Positions[0], 1e-4f);
        }

        [Test]
        public void Layout_DenseFromCountUnreachable_NeverGoesDense()
        {
            var result = SlotRowLayout.Layout(4, -127.5f, 150f, -95f, 112f, .82f, int.MaxValue);
            Assert.AreEqual(1f, result.Scale);
            Assert.AreEqual(-127.5f + 1.5f * 150f, result.Positions[3]);
        }
    }
}
