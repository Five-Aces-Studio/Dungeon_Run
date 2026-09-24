using System;
using DungeonRun.Rendering;
using NUnit.Framework;

public sealed class BrushNoiseBakerTests
{
    private const int Size = 64;

    [Test]
    public void SameSeed_IsIdentical()
    {
        var a = BrushNoiseBaker.BakeRgba32(Size, 12345);
        var b = BrushNoiseBaker.BakeRgba32(Size, 12345);
        CollectionAssert.AreEqual(a, b);
    }

    [Test]
    public void DifferentSeed_Differs()
    {
        var a = BrushNoiseBaker.BakeRgba32(Size, 1);
        var b = BrushNoiseBaker.BakeRgba32(Size, 2);
        CollectionAssert.AreNotEqual(a, b);
    }

    [Test]
    public void Length_IsSizeSquaredTimesFour()
    {
        var bytes = BrushNoiseBaker.BakeRgba32(Size, 7);
        Assert.AreEqual(Size * Size * 4, bytes.Length);
    }

    [Test]
    public void Alpha_IsAlways255()
    {
        var bytes = BrushNoiseBaker.BakeRgba32(Size, 7);
        for (int i = 3; i < bytes.Length; i += 4)
            Assert.AreEqual(255, bytes[i]);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void PerChannel_MeanInRangeAndStdDevAboveThreshold(int channel)
    {
        var bytes = BrushNoiseBaker.BakeRgba32(Size, 999);
        int count = Size * Size;
        double sum = 0;
        for (int p = channel; p < bytes.Length; p += 4) sum += bytes[p];
        double mean = sum / count;
        Assert.GreaterOrEqual(mean, 100.0);
        Assert.LessOrEqual(mean, 156.0);

        double variance = 0;
        for (int p = channel; p < bytes.Length; p += 4)
        {
            double d = bytes[p] - mean;
            variance += d * d;
        }
        double stdDev = Math.Sqrt(variance / count);
        Assert.Greater(stdDev, 25.0);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void SeamlessWrap_ColumnsAndRows(int channel)
    {
        var bytes = BrushNoiseBaker.BakeRgba32(Size, 4242);
        int At(int x, int y) => bytes[(y * Size + x) * 4 + channel];

        double wrapColumnDiff = 0;
        double interiorColumnDiff = 0;
        for (int y = 0; y < Size; y++)
        {
            wrapColumnDiff += Math.Abs(At(0, y) - At(Size - 1, y));
            for (int x = 0; x < Size - 1; x++)
                interiorColumnDiff += Math.Abs(At(x + 1, y) - At(x, y));
        }
        wrapColumnDiff /= Size;
        interiorColumnDiff /= (Size * (Size - 1));
        Assert.LessOrEqual(wrapColumnDiff, 1.5 * interiorColumnDiff);

        double wrapRowDiff = 0;
        double interiorRowDiff = 0;
        for (int x = 0; x < Size; x++)
        {
            wrapRowDiff += Math.Abs(At(x, 0) - At(x, Size - 1));
            for (int y = 0; y < Size - 1; y++)
                interiorRowDiff += Math.Abs(At(x, y + 1) - At(x, y));
        }
        wrapRowDiff /= Size;
        interiorRowDiff /= (Size * (Size - 1));
        Assert.LessOrEqual(wrapRowDiff, 1.5 * interiorRowDiff);
    }

    [TestCase(15)]
    [TestCase(17)]
    [TestCase(0)]
    [TestCase(-16)]
    public void InvalidSize_Throws(int size)
    {
        Assert.Throws<ArgumentException>(() => BrushNoiseBaker.BakeRgba32(size, 1));
    }
}
