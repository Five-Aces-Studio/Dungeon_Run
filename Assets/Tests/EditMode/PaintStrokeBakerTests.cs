using System;
using DungeonRun.Rendering;
using NUnit.Framework;

public sealed class PaintStrokeBakerTests
{
    private const int Size = 128;
    private const int ValueChannel = 0;
    private const int HueChannel = 1;
    private const int BristleChannel = 2;
    private const int BlotchChannel = 3;

    [TestCase(64)]
    [TestCase(128)]
    public void Length_IsSizeSquaredTimesFour(int size)
    {
        var bytes = PaintStrokeBaker.BakeRgba32(size, 7);
        Assert.AreEqual(size * size * 4, bytes.Length);
    }

    [Test]
    public void SameSeed_IsIdentical()
    {
        var a = PaintStrokeBaker.BakeRgba32(Size, 20260922);
        var b = PaintStrokeBaker.BakeRgba32(Size, 20260922);
        CollectionAssert.AreEqual(a, b);
    }

    [Test]
    public void DifferentSeed_Differs()
    {
        var a = PaintStrokeBaker.BakeRgba32(Size, 1);
        var b = PaintStrokeBaker.BakeRgba32(Size, 2);
        CollectionAssert.AreNotEqual(a, b);
    }

    [TestCase(0)]
    [TestCase(-64)]
    [TestCase(32)]
    [TestCase(63)]
    [TestCase(100)]
    public void InvalidSize_Throws(int size)
    {
        Assert.Throws<ArgumentException>(() => PaintStrokeBaker.BakeRgba32(size, 1));
    }

    // G = 0 is the reserved "never painted" sentinel; every covered pixel carries a hue id in 1..255. The baker also
    // throws if its own coverage check fails, so a successful bake with no zero G proves full dab coverage.
    [TestCase(64, 1)]
    [TestCase(64, 99)]
    [TestCase(128, 20260922)]
    [TestCase(256, 5)]
    public void EveryPixel_IsCoveredByADab(int size, int seed)
    {
        byte[] bytes = null;
        Assert.DoesNotThrow(() => bytes = PaintStrokeBaker.BakeRgba32(size, seed));
        for (int p = HueChannel; p < bytes.Length; p += 4)
            Assert.AreNotEqual(0, bytes[p], $"Uncovered pixel {(p - HueChannel) / 4}.");
    }

    [TestCase(128, 20260922)]
    [TestCase(256, 3)]
    public void Strokes_AreDominantlyHorizontal(int size, int seed)
    {
        var bytes = PaintStrokeBaker.BakeRgba32(size, seed);
        int At(int x, int y) => bytes[(y * size + x) * 4 + ValueChannel];

        double horizontal = 0, vertical = 0;
        for (int y = 0; y < size - 1; y++)
        {
            for (int x = 0; x < size - 1; x++)
            {
                horizontal += Math.Abs(At(x + 1, y) - At(x, y));
                vertical += Math.Abs(At(x, y + 1) - At(x, y));
            }
        }
        Assert.Less(horizontal, vertical);
    }

    [TestCase(ValueChannel)]
    [TestCase(BristleChannel)]
    [TestCase(BlotchChannel)]
    public void SeamlessWrap_ColumnsAndRows(int channel)
    {
        var bytes = PaintStrokeBaker.BakeRgba32(Size, 4242);
        int At(int x, int y) => bytes[(y * Size + x) * 4 + channel];

        double wrapColumnDiff = 0, interiorColumnDiff = 0;
        double wrapRowDiff = 0, interiorRowDiff = 0;
        for (int i = 0; i < Size; i++)
        {
            wrapColumnDiff += Math.Abs(At(0, i) - At(Size - 1, i));
            wrapRowDiff += Math.Abs(At(i, 0) - At(i, Size - 1));
            for (int k = 0; k < Size - 1; k++)
            {
                interiorColumnDiff += Math.Abs(At(k + 1, i) - At(k, i));
                interiorRowDiff += Math.Abs(At(i, k + 1) - At(i, k));
            }
        }
        wrapColumnDiff /= Size;
        wrapRowDiff /= Size;
        interiorColumnDiff /= Size * (Size - 1);
        interiorRowDiff /= Size * (Size - 1);
        Assert.LessOrEqual(wrapColumnDiff, 1.5 * interiorColumnDiff, "x wrap seam");
        Assert.LessOrEqual(wrapRowDiff, 1.5 * interiorRowDiff, "y wrap seam");
    }

    [TestCase(20260922)]
    [TestCase(11)]
    public void HueIds_AreRoughlyUniform(int seed)
    {
        var bytes = PaintStrokeBaker.BakeRgba32(Size, seed);
        int low = 0;
        for (int p = HueChannel; p < bytes.Length; p += 4)
            if (bytes[p] < 64) low++;
        double fraction = low / (double)(Size * Size);
        Assert.AreEqual(0.25, fraction, 0.08);
    }

    // Values are centre-weighted by design (triangular spread plus rare accents), so R spreads less than a uniform
    // distribution would (~32-40 instead of ~60); range usage is covered by
    // Values_AreCentreWeightedWithAccentsAtBothEnds.
    [TestCase(ValueChannel, 25.0)]
    [TestCase(BristleChannel, 12.0)]
    [TestCase(BlotchChannel, 20.0)]
    public void Channel_HasSpread(int channel, double minStdDev)
    {
        var bytes = PaintStrokeBaker.BakeRgba32(Size, 77);
        Assert.Greater(StdDev(bytes, channel), minStdDev);
    }

    [TestCase(ValueChannel)]
    [TestCase(BristleChannel)]
    public void CentredChannels_HaveMeanNearMidGrey(int channel)
    {
        var bytes = PaintStrokeBaker.BakeRgba32(Size, 77);
        Assert.AreEqual(127.5, Mean(bytes, channel), 20.0);
    }

    // Paint-like values: most dabs sit near the middle value (neighbours differ by modest steps), while occasional
    // accents still reach both ends of the normalized byte range.
    [TestCase(77)]
    [TestCase(20260922)]
    [TestCase(4242)]
    public void Values_AreCentreWeightedWithAccentsAtBothEnds(int seed)
    {
        var bytes = PaintStrokeBaker.BakeRgba32(Size, seed);
        int middle = 0, darkest = 255, lightest = 0;
        for (int p = ValueChannel; p < bytes.Length; p += 4)
        {
            int value = bytes[p];
            if (value >= 64 && value < 192) middle++;
            darkest = Math.Min(darkest, value);
            lightest = Math.Max(lightest, value);
        }
        Assert.Greater(middle / (double)(Size * Size), 0.72, "fraction in the middle half of the range");
        Assert.LessOrEqual(darkest, 32, "dark accents");
        Assert.GreaterOrEqual(lightest, 224, "light accents");
    }

    // Bristle streaks stay visible but subtle: lower contrast than the value channel, centred on mid grey.
    [TestCase(77)]
    [TestCase(4242)]
    public void Bristle_IsLowContrast(int seed)
    {
        var bytes = PaintStrokeBaker.BakeRgba32(Size, seed);
        double stdDev = StdDev(bytes, BristleChannel);
        Assert.Greater(stdDev, 12.0);
        Assert.Less(stdDev, 28.0);
        Assert.Less(stdDev, StdDev(bytes, ValueChannel));
    }

    private static double Mean(byte[] bytes, int channel)
    {
        double sum = 0;
        for (int p = channel; p < bytes.Length; p += 4) sum += bytes[p];
        return sum / (bytes.Length / 4);
    }

    private static double StdDev(byte[] bytes, int channel)
    {
        double mean = Mean(bytes, channel);
        double variance = 0;
        for (int p = channel; p < bytes.Length; p += 4)
        {
            double d = bytes[p] - mean;
            variance += d * d;
        }
        return Math.Sqrt(variance / (bytes.Length / 4));
    }
}
