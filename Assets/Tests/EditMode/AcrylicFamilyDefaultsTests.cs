using DungeonRun.Rendering;
using NUnit.Framework;

public sealed class AcrylicFamilyDefaultsTests
{
    [Test]
    public void EveryAcrylicNameHasDefaults()
    {
        foreach (var name in AcrylicMaterialMap.AllAcrylicNames)
        {
            Assert.IsTrue(AcrylicFamilyDefaults.TryGet(name, out _), $"Missing defaults for {name}.");
        }
    }

    [TestCase("TravelerCloth_AcrylicV3")]
    [TestCase("TravelerArmor_AcrylicV3")]
    [TestCase("EnemyBone_AcrylicV3")]
    [TestCase("EnemyClay_AcrylicV3")]
    [TestCase("EnemyJade_AcrylicV3")]
    [TestCase("CharacterMetal_AcrylicV3")]
    [TestCase("CreatureStone_AcrylicV3")]
    [TestCase("GuardianShield_AcrylicV3")]
    [TestCase("HoundClay_AcrylicV3")]
    public void Characters_AreObjectTriplanarWithZeroShadowBreakup(string name)
    {
        var value = AcrylicFamilyDefaults.Get(name);
        Assert.AreEqual(AcrylicBrushMapping.ObjectTriplanar, value.BrushMapping);
        Assert.AreEqual(0f, value.ShadowBreakup, 1e-6f);
    }

    [TestCase("StoneFloor_AcrylicV3")]
    [TestCase("StoneDark_AcrylicV3")]
    [TestCase("StoneWorn_AcrylicV3")]
    [TestCase("StoneBackground_AcrylicV3")]
    public void Stone_IsWorldTriplanarWithPositiveShadowBreakup(string name)
    {
        var value = AcrylicFamilyDefaults.Get(name);
        Assert.AreEqual(AcrylicBrushMapping.WorldTriplanar, value.BrushMapping);
        Assert.Greater(value.ShadowBreakup, 0f);
    }

    [Test]
    public void ArcaneWarmTorch_IsWorldTriplanarWithBoostedEmission()
    {
        var value = AcrylicFamilyDefaults.Get("ArcaneWarmTorch_AcrylicV3");
        Assert.AreEqual(AcrylicBrushMapping.WorldTriplanar, value.BrushMapping);
        Assert.AreEqual(1.6f, value.EmissionScale, 1e-6f);
    }

    [Test]
    public void ArcaneWarm_IsObjectTriplanarWithUnscaledEmission()
    {
        var value = AcrylicFamilyDefaults.Get("ArcaneWarm_AcrylicV3");
        Assert.AreEqual(AcrylicBrushMapping.ObjectTriplanar, value.BrushMapping);
        Assert.AreEqual(1f, value.EmissionScale, 1e-6f);
    }

    // Full per-family tuple check against the spec table (strength, scale, wrap, shadowBreakup, rim, minBand0,
    // ambientScale, emissionScale), so a future transcription error in any of the 17 x 8 numbers is caught instead
    // of only the handful of fields the tests above spot-check.
    [TestCase("StoneFloor_AcrylicV3", AcrylicBrushMapping.WorldTriplanar, .65f, .35f, .15f, 1f, 0f, .16f, 1.25f, 1f)]
    [TestCase("StoneDark_AcrylicV3", AcrylicBrushMapping.WorldTriplanar, .6f, .4f, .15f, 1f, 0f, .16f, 1.25f, 1f)]
    [TestCase("StoneWorn_AcrylicV3", AcrylicBrushMapping.WorldTriplanar, .65f, .4f, .15f, 1f, 0f, .16f, 1.25f, 1f)]
    [TestCase("StoneBackground_AcrylicV3", AcrylicBrushMapping.WorldTriplanar, .4f, .25f, .15f, .8f, 0f, .14f, 1.15f, 1f)]
    [TestCase("MetalDark_AcrylicV3", AcrylicBrushMapping.WorldTriplanar, .18f, .6f, 0f, .5f, 0f, .12f, 1f, 1f)]
    [TestCase("ArcaneDistant_AcrylicV3", AcrylicBrushMapping.WorldTriplanar, .1f, 1f, 0f, 0f, 0f, 0f, 1f, 1f)]
    [TestCase("ArcaneWarm_AcrylicV3", AcrylicBrushMapping.ObjectTriplanar, .1f, 1f, 0f, 0f, 0f, 0f, 1f, 1f)]
    [TestCase("ArcaneCool_AcrylicV3", AcrylicBrushMapping.ObjectTriplanar, .1f, 1f, 0f, 0f, 0f, 0f, 1f, 1f)]
    [TestCase("ArcaneWarmTorch_AcrylicV3", AcrylicBrushMapping.WorldTriplanar, .1f, 1f, 0f, 0f, 0f, 0f, 1f, 1.6f)]
    [TestCase("TravelerCloth_AcrylicV3", AcrylicBrushMapping.ObjectTriplanar, .45f, 1.2f, .5f, 0f, .35f, .16f, 1.1f, 1f)]
    [TestCase("TravelerArmor_AcrylicV3", AcrylicBrushMapping.ObjectTriplanar, .3f, 1.2f, .2f, 0f, .35f, .16f, 1.1f, 1f)]
    [TestCase("EnemyBone_AcrylicV3", AcrylicBrushMapping.ObjectTriplanar, .3f, 1.2f, .25f, 0f, .3f, .16f, 1.1f, 1f)]
    [TestCase("EnemyClay_AcrylicV3", AcrylicBrushMapping.ObjectTriplanar, .5f, 1.2f, .25f, 0f, .3f, .16f, 1.1f, 1f)]
    [TestCase("EnemyJade_AcrylicV3", AcrylicBrushMapping.ObjectTriplanar, .3f, 1.2f, .2f, 0f, .3f, .16f, 1.1f, 1f)]
    [TestCase("CharacterMetal_AcrylicV3", AcrylicBrushMapping.ObjectTriplanar, .18f, 1.5f, 0f, 0f, .3f, .16f, 1f, 1f)]
    [TestCase("CreatureStone_AcrylicV3", AcrylicBrushMapping.ObjectTriplanar, .3f, 1.2f, .15f, 0f, .3f, .16f, 1.1f, 1f)]
    [TestCase("GuardianShield_AcrylicV3", AcrylicBrushMapping.ObjectTriplanar, .3f, 1.2f, .15f, 0f, .3f, .16f, 1.1f, 1f)]
    [TestCase("HoundClay_AcrylicV3", AcrylicBrushMapping.ObjectTriplanar, .5f, 1.2f, .25f, 0f, .3f, .16f, 1.1f, 1f)]
    public void FamilyDefaults_MatchSpecTable(string name, AcrylicBrushMapping mapping, float strength, float scale,
        float wrap, float shadowBreakup, float rim, float minBand0, float ambientScale, float emissionScale)
    {
        var value = AcrylicFamilyDefaults.Get(name);
        Assert.AreEqual(mapping, value.BrushMapping, $"{name}.BrushMapping");
        Assert.AreEqual(strength, value.BrushStrength, 1e-6f, $"{name}.BrushStrength");
        Assert.AreEqual(scale, value.BrushScale, 1e-6f, $"{name}.BrushScale");
        Assert.AreEqual(wrap, value.LightWrap, 1e-6f, $"{name}.LightWrap");
        Assert.AreEqual(shadowBreakup, value.ShadowBreakup, 1e-6f, $"{name}.ShadowBreakup");
        Assert.AreEqual(rim, value.RimStrength, 1e-6f, $"{name}.RimStrength");
        Assert.AreEqual(minBand0, value.MinBandLevel0, 1e-6f, $"{name}.MinBandLevel0");
        Assert.AreEqual(ambientScale, value.AmbientScale, 1e-6f, $"{name}.AmbientScale");
        Assert.AreEqual(emissionScale, value.EmissionScale, 1e-6f, $"{name}.EmissionScale");
    }
}
