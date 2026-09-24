using DungeonRun.Rendering;
using NUnit.Framework;

public sealed class PainterlyGlobalsTests
{
    private const float Tolerance = 1e-4f;

    [Test]
    public void Neutral_IsAllZero()
    {
        Assert.AreEqual(0f, PainterlyGlobals.Neutral.Painterly.X, Tolerance);
        Assert.AreEqual(0f, PainterlyGlobals.Neutral.Painterly.Y, Tolerance);
        Assert.AreEqual(0f, PainterlyGlobals.Neutral.Painterly.Z, Tolerance);
        Assert.AreEqual(0f, PainterlyGlobals.Neutral.Painterly.W, Tolerance);
        Assert.AreEqual(0f, PainterlyGlobals.Neutral.Brush.X, Tolerance);
        Assert.AreEqual(0f, PainterlyGlobals.Neutral.Brush.Y, Tolerance);
        Assert.AreEqual(0f, PainterlyGlobals.Neutral.Brush.Z, Tolerance);
        Assert.AreEqual(0f, PainterlyGlobals.Neutral.Brush.W, Tolerance);
    }

    [Test]
    public void InfluenceZero_ProducesNeutral()
    {
        var globals = PainterlyGlobals.Create(0f, 1f, 1f, 1f, 0.5f, 1f, 1f, 0.5f);
        Assert.AreEqual(PainterlyGlobals.Neutral, globals);
    }

    [Test]
    public void InfluenceOne_PreservesEveryComponentAtItsMax()
    {
        var globals = PainterlyGlobals.Create(1f, 1f, 1f, 1f, 0.5f, 1f, 1f, 0.5f);
        Assert.AreEqual(1f, globals.Painterly.X, Tolerance);
        Assert.AreEqual(1f, globals.Painterly.Y, Tolerance);
        Assert.AreEqual(1f, globals.Painterly.Z, Tolerance);
        Assert.AreEqual(1f, globals.Painterly.W, Tolerance);
        Assert.AreEqual(0.5f, globals.Brush.X, Tolerance);
        Assert.AreEqual(1f, globals.Brush.Y, Tolerance);
        Assert.AreEqual(1f, globals.Brush.Z, Tolerance);
        Assert.AreEqual(0.5f, globals.Brush.W, Tolerance);
    }

    [Test]
    public void Influence_ScalesPainterlyYzwAndAllOfBrush()
    {
        var globals = PainterlyGlobals.Create(0.5f, 0.8f, 0.6f, 0.4f, 0.2f, 0.6f, 0.4f, 0.2f);
        Assert.AreEqual(0.5f, globals.Painterly.X, Tolerance);
        Assert.AreEqual(0.4f, globals.Painterly.Y, Tolerance);
        Assert.AreEqual(0.3f, globals.Painterly.Z, Tolerance);
        Assert.AreEqual(0.2f, globals.Painterly.W, Tolerance);
        Assert.AreEqual(0.1f, globals.Brush.X, Tolerance);
        Assert.AreEqual(0.3f, globals.Brush.Y, Tolerance);
        Assert.AreEqual(0.2f, globals.Brush.Z, Tolerance);
        Assert.AreEqual(0.1f, globals.Brush.W, Tolerance);
    }

    [TestCase(-1f, 0f)]
    [TestCase(2f, 1f)]
    public void Influence_Clamped01(float input, float expected)
    {
        var globals = PainterlyGlobals.Create(input, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
        Assert.AreEqual(expected, globals.Painterly.X, Tolerance);
    }

    [TestCase(-1f, 0f)]
    [TestCase(2f, 1f)]
    public void LightingSmoothness_ClampedThenScaledByInfluence(float input, float expectedClamped)
    {
        var globals = PainterlyGlobals.Create(1f, input, 0f, 0f, 0f, 0f, 0f, 0f);
        Assert.AreEqual(expectedClamped, globals.Painterly.Y, Tolerance);
    }

    [TestCase(-1f, 0f)]
    [TestCase(0.6f, 0.5f)]
    public void AlbedoVariation_ClampedTo0Point5(float input, float expected)
    {
        var globals = PainterlyGlobals.Create(1f, 0f, 0f, 0f, input, 0f, 0f, 0f);
        Assert.AreEqual(expected, globals.Brush.X, Tolerance);
    }

    [TestCase(-1f, 0f)]
    [TestCase(0.6f, 0.5f)]
    public void ShadowEdgeBreakup_ClampedTo0Point5(float input, float expected)
    {
        var globals = PainterlyGlobals.Create(1f, 0f, 0f, 0f, 0f, 0f, 0f, input);
        Assert.AreEqual(expected, globals.Brush.W, Tolerance);
    }

    [TestCase(-1f, 0f)]
    [TestCase(2f, 1f)]
    public void ColorVariation_Clamped01(float input, float expected)
    {
        var globals = PainterlyGlobals.Create(1f, 0f, 0f, 0f, 0f, input, 0f, 0f);
        Assert.AreEqual(expected, globals.Brush.Y, Tolerance);
    }

    [TestCase(-1f, 0f)]
    [TestCase(2f, 1f)]
    public void TerminatorBreakup_Clamped01(float input, float expected)
    {
        var globals = PainterlyGlobals.Create(1f, 0f, 0f, 0f, 0f, 0f, input, 0f);
        Assert.AreEqual(expected, globals.Brush.Z, Tolerance);
    }
}
