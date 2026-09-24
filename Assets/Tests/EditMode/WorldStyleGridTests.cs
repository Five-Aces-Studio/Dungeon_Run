using System;
using DungeonRun.Rendering;
using NUnit.Framework;

public sealed class WorldStyleGridTests
{
    [TestCase(2f, 1080, 2)]
    [TestCase(2f, 1440, 3)]
    [TestCase(4f, 1080, 4)]
    [TestCase(1.75f, 1080, 2)]
    [TestCase(1.75f, 1440, 2)]
    [TestCase(1f, 720, 1)]
    [TestCase(0.2f, 1080, 1)]
    public void CellPixels_MatchesDocumentedMapping(float pixelScale, int outputHeight, int expected)
    {
        Assert.AreEqual(expected, WorldStyleGrid.CellPixels(pixelScale, outputHeight));
    }

    [TestCase(2f, 0)]
    [TestCase(2f, -10)]
    public void CellPixels_NonPositiveOutputHeight_ReturnsOne(float pixelScale, int outputHeight)
    {
        Assert.AreEqual(1, WorldStyleGrid.CellPixels(pixelScale, outputHeight));
    }

    [Test]
    public void CellPixels_NaNPixelScale_ReturnsOne()
    {
        Assert.AreEqual(1, WorldStyleGrid.CellPixels(float.NaN, 1080));
    }

    [TestCase(0f)]
    [TestCase(-3f)]
    public void CellPixels_NonPositivePixelScale_ReturnsOne(float pixelScale)
    {
        Assert.AreEqual(1, WorldStyleGrid.CellPixels(pixelScale, 1080));
    }

    [Test]
    public void CellPixels_InfinitePixelScale_ReturnsOne()
    {
        Assert.AreEqual(1, WorldStyleGrid.CellPixels(float.PositiveInfinity, 1080));
        Assert.AreEqual(1, WorldStyleGrid.CellPixels(float.NegativeInfinity, 1080));
    }

    [Test]
    public void CellPixels_NeverReturnsLessThanOne()
    {
        for (int h = -5; h <= 5; h++)
            Assert.GreaterOrEqual(WorldStyleGrid.CellPixels(0.01f, h), 1);
    }
}
