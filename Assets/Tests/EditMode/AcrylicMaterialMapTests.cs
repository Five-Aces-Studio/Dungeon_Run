using DungeonRun.Rendering;
using NUnit.Framework;

public sealed class AcrylicMaterialMapTests
{
    [TestCase("StoneFloor_StylizedV1", "StoneFloor_AcrylicV3")]
    [TestCase("StoneDark_StylizedV1", "StoneDark_AcrylicV3")]
    [TestCase("StoneWorn_StylizedV1", "StoneWorn_AcrylicV3")]
    [TestCase("StoneBackground_StylizedV1", "StoneBackground_AcrylicV3")]
    [TestCase("MetalDark_StylizedV1", "MetalDark_AcrylicV3")]
    [TestCase("TravelerCloth_StylizedV1", "TravelerCloth_AcrylicV3")]
    [TestCase("TravelerArmor_StylizedV1", "TravelerArmor_AcrylicV3")]
    [TestCase("EnemyBone_StylizedV1", "EnemyBone_AcrylicV3")]
    [TestCase("EnemyClay_StylizedV1", "EnemyClay_AcrylicV3")]
    [TestCase("EnemyJade_StylizedV1", "EnemyJade_AcrylicV3")]
    [TestCase("ArcaneWarm_StylizedV1", "ArcaneWarm_AcrylicV3")]
    [TestCase("ArcaneCool_StylizedV1", "ArcaneCool_AcrylicV3")]
    [TestCase("ArcaneDistant_StylizedV1", "ArcaneDistant_AcrylicV3")]
    [TestCase("V2_CharacterMetal", "CharacterMetal_AcrylicV3")]
    [TestCase("V2_CreatureStone", "CreatureStone_AcrylicV3")]
    [TestCase("V2_GuardianShield", "GuardianShield_AcrylicV3")]
    [TestCase("V2_HoundClay", "HoundClay_AcrylicV3")]
    public void Resolve_AllSourcesMapToTheirFamily(string source, string expectedAcrylic)
    {
        Assert.AreEqual(expectedAcrylic, AcrylicMaterialMap.Resolve("ENV_SomeRenderer", source));
    }

    [Test]
    public void Resolve_SeventeenSourceNamesAreCovered()
    {
        Assert.AreEqual(17, AcrylicMaterialMap.SourceNames.Count);
        foreach (var source in AcrylicMaterialMap.SourceNames)
            Assert.IsNotNull(AcrylicMaterialMap.Resolve("ENV_SomeRenderer", source), $"{source} did not resolve.");
    }

    [TestCase("ENV_ArcaneFixture_1")]
    [TestCase("ENV_ArcaneFixture_2")]
    public void Resolve_FixtureRenderersOverrideToTorch(string rendererName)
    {
        Assert.AreEqual("ArcaneWarmTorch_AcrylicV3", AcrylicMaterialMap.Resolve(rendererName, "ArcaneWarm_StylizedV1"));
    }

    [Test]
    public void Resolve_SentinelRendererIsNotOverridden()
    {
        Assert.AreEqual("ArcaneWarm_AcrylicV3", AcrylicMaterialMap.Resolve("CHR_Enemy_03", "ArcaneWarm_StylizedV1"));
    }

    [Test]
    public void Resolve_UnknownSourceReturnsNull()
    {
        Assert.IsNull(AcrylicMaterialMap.Resolve("ENV_SomeRenderer", "Nonexistent_StylizedV1"));
    }

    [TestCase("StoneFloor_V1")]
    [TestCase("V1_StoneFloor")]
    public void Resolve_LegacyV1SourcesAreUnmapped(string source)
    {
        Assert.IsNull(AcrylicMaterialMap.Resolve("ENV_SomeRenderer", source));
    }

    [Test]
    public void InverseRoundTrip_HoldsForEveryKnownSource()
    {
        foreach (var source in AcrylicMaterialMap.SourceNames)
        {
            var acrylic = AcrylicMaterialMap.Resolve("ENV_SomeRenderer", source);
            Assert.AreEqual(source, AcrylicMaterialMap.Inverse(acrylic));
        }
    }

    [TestCase("ENV_ArcaneFixture_1")]
    [TestCase("ENV_ArcaneFixture_2")]
    public void InverseRoundTrip_HoldsForFixtureOverride(string rendererName)
    {
        var acrylic = AcrylicMaterialMap.Resolve(rendererName, "ArcaneWarm_StylizedV1");
        Assert.AreEqual("ArcaneWarm_StylizedV1", AcrylicMaterialMap.Inverse(acrylic));
    }

    [Test]
    public void Inverse_UnknownReturnsNull()
    {
        Assert.IsNull(AcrylicMaterialMap.Inverse("NotARealFamily_AcrylicV3"));
    }

    [Test]
    public void IsAcrylic_TrueForEveryAllAcrylicName()
    {
        foreach (var name in AcrylicMaterialMap.AllAcrylicNames)
            Assert.IsTrue(AcrylicMaterialMap.IsAcrylic(name));
    }

    [Test]
    public void IsAcrylic_FalseForUnrelatedName()
    {
        Assert.IsFalse(AcrylicMaterialMap.IsAcrylic("StoneFloor_StylizedV1"));
    }

    [Test]
    public void AllAcrylicNames_HasEighteenEntries()
    {
        Assert.AreEqual(18, AcrylicMaterialMap.AllAcrylicNames.Count);
    }
}
