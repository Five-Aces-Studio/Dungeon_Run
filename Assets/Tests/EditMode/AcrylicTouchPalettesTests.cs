using System.Collections.Generic;
using DungeonRun.Rendering;
using NUnit.Framework;

public sealed class AcrylicTouchPalettesTests
{
    [Test]
    public void EveryAcrylicNameHasAPalette()
    {
        foreach (var name in AcrylicMaterialMap.AllAcrylicNames)
            Assert.IsTrue(AcrylicTouchPalettes.TryGet(name, out _), $"Missing touch palette for {name}.");
    }

    [Test]
    public void Amounts_AreBounded()
    {
        foreach (var name in AcrylicMaterialMap.AllAcrylicNames)
        {
            var palette = AcrylicTouchPalettes.Get(name);
            Assert.That(palette.AmountA, Is.InRange(0f, 0.35f), $"{name}.AmountA");
            Assert.That(palette.AmountB, Is.InRange(0f, 0.35f), $"{name}.AmountB");
            Assert.LessOrEqual(palette.AmountA + palette.AmountB, 0.5f, $"{name} amount sum");
        }
    }

    [Test]
    public void ColoursAndStrength_AreInUnitRange()
    {
        foreach (var name in AcrylicMaterialMap.AllAcrylicNames)
        {
            var p = AcrylicTouchPalettes.Get(name);
            float[] values = { p.ARed, p.AGreen, p.ABlue, p.BRed, p.BGreen, p.BBlue, p.Strength };
            foreach (float v in values)
                Assert.That(v, Is.InRange(0f, 1f), name);
        }
    }

    [TestCase("ArcaneWarm_AcrylicV3")]
    [TestCase("ArcaneCool_AcrylicV3")]
    [TestCase("ArcaneWarmTorch_AcrylicV3")]
    [TestCase("ArcaneDistant_AcrylicV3")]
    public void Emissive_HasNoTouches(string name)
    {
        var palette = AcrylicTouchPalettes.Get(name);
        Assert.AreEqual(0f, palette.AmountA, $"{name}.AmountA");
        Assert.AreEqual(0f, palette.AmountB, $"{name}.AmountB");
    }

    [TestCase("StoneFloor_AcrylicV3")]
    [TestCase("StoneDark_AcrylicV3")]
    [TestCase("StoneWorn_AcrylicV3")]
    [TestCase("StoneBackground_AcrylicV3")]
    [TestCase("MetalDark_AcrylicV3")]
    [TestCase("TravelerCloth_AcrylicV3")]
    [TestCase("TravelerArmor_AcrylicV3")]
    [TestCase("EnemyBone_AcrylicV3")]
    [TestCase("EnemyClay_AcrylicV3")]
    [TestCase("EnemyJade_AcrylicV3")]
    [TestCase("CharacterMetal_AcrylicV3")]
    [TestCase("CreatureStone_AcrylicV3")]
    [TestCase("GuardianShield_AcrylicV3")]
    [TestCase("HoundClay_AcrylicV3")]
    public void PaintedSurfaces_HaveVisibleTouches(string name)
    {
        var palette = AcrylicTouchPalettes.Get(name);
        Assert.Greater(palette.AmountA + palette.AmountB, 0f, $"{name} amount sum");
        Assert.Greater(palette.Strength, 0f, $"{name}.Strength");
    }

    [Test]
    public void Get_MatchesTryGet()
    {
        foreach (var name in AcrylicMaterialMap.AllAcrylicNames)
        {
            Assert.IsTrue(AcrylicTouchPalettes.TryGet(name, out var fromTry));
            Assert.AreEqual(fromTry, AcrylicTouchPalettes.Get(name), name);
        }
    }

    [TestCase("Unknown_AcrylicV3")]
    [TestCase("StoneFloor_StylizedV1")]
    [TestCase("")]
    public void Get_UnknownName_Throws(string name)
    {
        Assert.Throws<KeyNotFoundException>(() => AcrylicTouchPalettes.Get(name));
    }

    [Test]
    public void Get_Null_ThrowsKeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(() => AcrylicTouchPalettes.Get(null));
    }

    [Test]
    public void TryGet_UnknownOrNull_ReturnsFalse()
    {
        Assert.IsFalse(AcrylicTouchPalettes.TryGet("Unknown_AcrylicV3", out _));
        Assert.IsFalse(AcrylicTouchPalettes.TryGet(null, out _));
    }

    [Test]
    public void TouchPalette_HasValueEquality()
    {
        var a = new TouchPalette(.5f, .5f, .25f, .52f, .33f, .24f, .2f, .08f, .75f);
        var b = new TouchPalette(.5f, .5f, .25f, .52f, .33f, .24f, .2f, .08f, .75f);
        var c = new TouchPalette(.5f, .5f, .25f, .52f, .33f, .24f, .2f, .08f, .7f);
        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.AreNotEqual(a, c);
    }
}
