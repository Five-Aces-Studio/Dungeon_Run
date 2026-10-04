using NUnit.Framework;

namespace DungeonRun.UI.Presentation.Tests
{
    public sealed class HudFeedbackFormatTests
    {
        [TestCase("-1", FeedbackGlyph.Damage)]
        [TestCase("-12", FeedbackGlyph.Damage)]
        [TestCase("PIERCED 2", FeedbackGlyph.Pierce)]
        [TestCase("BLOCK 1", FeedbackGlyph.Block)]
        [TestCase("DODGE", FeedbackGlyph.Dodge)]
        [TestCase("+3", FeedbackGlyph.Heal)]
        [TestCase("CHARGED", FeedbackGlyph.Charge)]
        [TestCase("DEFEATED", FeedbackGlyph.Defeat)]
        public void Glyph_MapsFeedbackStrings(string message, FeedbackGlyph expected) =>
            Assert.AreEqual(expected, HudFeedbackFormat.Glyph(message));

        [TestCase("")]
        [TestCase(null)]
        [TestCase("SOMETHING ELSE")]
        public void Glyph_Unknown_IsNone(string message) => Assert.AreEqual(FeedbackGlyph.None, HudFeedbackFormat.Glyph(message));

        [TestCase("-1", true)]
        [TestCase("+2", true)]
        [TestCase("PIERCED 2", false)]
        [TestCase("BLOCK 1", false)]
        [TestCase("DODGE", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsNumber_OnlySignedAmounts(string message, bool expected) =>
            Assert.AreEqual(expected, HudFeedbackFormat.IsNumber(message));

        [Test]
        public void FeedbackGlyph_OrderMatchesStyleArray()
        {
            var order = new[] { "Damage", "Pierce", "Block", "Dodge", "Heal", "Charge", "Defeat" };
            for (var i = 0; i < order.Length; i++) Assert.AreEqual(order[i], ((FeedbackGlyph)i).ToString());
            Assert.AreEqual(-1, (int)FeedbackGlyph.None);
            Assert.AreEqual(order.Length + 1, System.Enum.GetValues(typeof(FeedbackGlyph)).Length);
        }

        [Test]
        public void FeedbackGlyph_ArrayIndexStartsAtZeroForDamage() => Assert.AreEqual(0, (int)FeedbackGlyph.Damage);
    }
}
