using NUnit.Framework;

namespace DungeonRun.UI.Presentation.Tests
{
    public sealed class CardCategoryMapTests
    {
        // PreviewCardKind order: Attack, Defence, Dodge, Heal, Piercing, Combo, Counterattack, Charge, Miss.
        [TestCase(0, CardCategory.Attack)]
        [TestCase(1, CardCategory.Defence)]
        [TestCase(2, CardCategory.Mobility)]
        [TestCase(3, CardCategory.Support)]
        [TestCase(4, CardCategory.Attack)]
        [TestCase(5, CardCategory.Attack)]
        [TestCase(6, CardCategory.Mobility)]
        [TestCase(7, CardCategory.Special)]
        [TestCase(8, CardCategory.Special)]
        public void Category_MapsEveryPreviewKind(int kind, CardCategory expected) =>
            Assert.AreEqual(expected, CardCategoryMap.Category(kind));

        [TestCase(-1)]
        [TestCase(9)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void Category_OutOfRange_IsAttack(int kind) =>
            Assert.AreEqual(CardCategory.Attack, CardCategoryMap.Category(kind));

        [TestCase(0, "ATTACK")]
        [TestCase(1, "DEFENCE")]
        [TestCase(2, "MOBILITY")]
        [TestCase(3, "SUPPORT")]
        [TestCase(4, "ATTACK")]
        [TestCase(5, "ATTACK")]
        [TestCase(6, "MOBILITY")]
        [TestCase(7, "SPECIAL")]
        [TestCase(8, "NO EFFECT")]
        public void FooterLabel_MapsEveryPreviewKind(int kind, string expected) =>
            Assert.AreEqual(expected, CardCategoryMap.FooterLabel(kind));

        [TestCase(-1)]
        [TestCase(9)]
        public void FooterLabel_OutOfRange_IsAttack(int kind) =>
            Assert.AreEqual("ATTACK", CardCategoryMap.FooterLabel(kind));

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        public void GlyphIndex_IsCategoryOrdinal(int kind) =>
            Assert.AreEqual((int)CardCategoryMap.Category(kind), CardCategoryMap.GlyphIndex(kind));

        [Test]
        public void GlyphIndex_CoversAllFiveGlyphs()
        {
            Assert.AreEqual(0, CardCategoryMap.GlyphIndex(0));
            Assert.AreEqual(1, CardCategoryMap.GlyphIndex(1));
            Assert.AreEqual(2, CardCategoryMap.GlyphIndex(2));
            Assert.AreEqual(3, CardCategoryMap.GlyphIndex(3));
            Assert.AreEqual(4, CardCategoryMap.GlyphIndex(7));
        }

        [Test]
        public void CardCategory_OrderIsStable() =>
            CollectionAssert.AreEqual(new[] { "Attack", "Defence", "Mobility", "Support", "Special" },
                System.Enum.GetNames(typeof(CardCategory)));
    }
}
