using DungeonRun.Rendering;
using NUnit.Framework;

public sealed class PaintFilterMathTests
{
    [TestCase(4f, 1080, true, 2f)]
    [TestCase(4f, 1440, true, 2.6666667f)]
    [TestCase(4f, 1080, false, 4f)]
    [TestCase(3f, 2160, true, 3f)]
    public void KuwaharaRadius_ScalesWithHeightAndResolution(float radius1080, int height, bool half, float expected)
    {
        Assert.AreEqual(expected, PaintFilterMath.KuwaharaRadius(radius1080, height, half), 1e-4f);
    }

    [TestCase(1f, 720, true)]
    [TestCase(0f, 1080, false)]
    [TestCase(-5f, 1080, true)]
    public void KuwaharaRadius_ClampsToOne(float radius1080, int height, bool half)
    {
        Assert.AreEqual(1f, PaintFilterMath.KuwaharaRadius(radius1080, height, half));
    }

    [TestCase(8f, 2160, false)]
    [TestCase(8f, 4320, true)]
    [TestCase(100f, 1080, true)]
    public void KuwaharaRadius_ClampsToMaxRadius(float radius1080, int height, bool half)
    {
        Assert.AreEqual(PaintFilterMath.MaxRadius, PaintFilterMath.KuwaharaRadius(radius1080, height, half));
        Assert.AreEqual(8f, PaintFilterMath.MaxRadius);
    }

    [TestCase(0)]
    [TestCase(-1080)]
    public void KuwaharaRadius_InvalidHeight_ReturnsOne(int height)
    {
        Assert.AreEqual(1f, PaintFilterMath.KuwaharaRadius(4f, height, true));
    }

    [Test]
    public void KuwaharaRadius_NonFiniteRadius_ReturnsOne()
    {
        Assert.AreEqual(1f, PaintFilterMath.KuwaharaRadius(float.NaN, 1080, true));
        Assert.AreEqual(1f, PaintFilterMath.KuwaharaRadius(float.PositiveInfinity, 1080, true));
        Assert.AreEqual(1f, PaintFilterMath.KuwaharaRadius(float.NegativeInfinity, 1080, true));
    }

    [TestCase(1920, 960)]
    [TestCase(1080, 540)]
    [TestCase(1081, 541)]
    [TestCase(1, 1)]
    [TestCase(2, 1)]
    [TestCase(0, 1)]
    [TestCase(-7, 1)]
    public void HalfSize_RoundsUpAndNeverDropsBelowOne(int fullSize, int expected)
    {
        Assert.AreEqual(expected, PaintFilterMath.HalfSize(fullSize));
    }
}
