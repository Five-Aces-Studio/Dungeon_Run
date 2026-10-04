using DungeonRun.UI.Presentation;
using NUnit.Framework;

public sealed class EnemyVisualScaleRulesTests
{
    [Test]
    public void Resolve_KeepsValuesInsideRange() => Assert.AreEqual(1.3f, EnemyVisualScaleRules.Resolve(1.3f), 1e-6f);

    [TestCase(0f, EnemyVisualScaleRules.Min)]
    [TestCase(-2f, EnemyVisualScaleRules.Min)]
    [TestCase(99f, EnemyVisualScaleRules.Max)]
    public void Resolve_ClampsOutOfRange(float requested, float expected) =>
        Assert.AreEqual(expected, EnemyVisualScaleRules.Resolve(requested), 1e-6f);

    [Test]
    public void Resolve_NaNFallsBackToOne() => Assert.AreEqual(1f, EnemyVisualScaleRules.Resolve(float.NaN), 1e-6f);

    [Test]
    public void ForHeightRatio_ScalesBaseHeightToRatioOfReference()
    {
        // A 3.0 m model that must stand 1.5x a 2.9 m protagonist.
        float scale = EnemyVisualScaleRules.ForHeightRatio(1.5f, 2.9f, 3.0f);
        Assert.AreEqual(1.45f, scale, 1e-5f);
        Assert.AreEqual(1.5f * 2.9f, scale * 3.0f, 1e-5f);
    }

    [Test]
    public void ForHeightRatio_InvalidHeightsFallBackToOne() =>
        Assert.AreEqual(1f, EnemyVisualScaleRules.ForHeightRatio(1.5f, 2.9f, 0f), 1e-6f);
}
