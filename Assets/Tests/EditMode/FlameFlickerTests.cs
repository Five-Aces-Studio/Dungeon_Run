using System;
using DungeonRun.Rendering;
using NUnit.Framework;

public sealed class FlameFlickerTests
{
    private const float SampleStep = 0.005f;

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(1020)]
    [TestCase(-77)]
    [TestCase(int.MaxValue)]
    public void Evaluate_StaysWithinUnitRange(int seed)
    {
        for (int i = 0; i < 20000; i++)
        {
            float value = FlameFlicker.Evaluate(i * SampleStep, seed);
            Assert.GreaterOrEqual(value, -1f, $"t={i * SampleStep}");
            Assert.LessOrEqual(value, 1f, $"t={i * SampleStep}");
        }
    }

    [Test]
    public void Evaluate_IsDeterministic()
    {
        for (int i = 0; i < 2000; i++)
        {
            float t = i * 0.0173f;
            Assert.AreEqual(FlameFlicker.Evaluate(t, 1013), FlameFlicker.Evaluate(t, 1013));
        }
    }

    [Test]
    public void DifferentSeeds_GiveDifferentSequences()
    {
        double meanAbsDiff = 0;
        const int count = 4000;
        for (int i = 0; i < count; i++)
        {
            float t = i * 0.01f;
            meanAbsDiff += Math.Abs(FlameFlicker.Evaluate(t, 1020) - FlameFlicker.Evaluate(t, 2033));
        }
        meanAbsDiff /= count;
        Assert.Greater(meanAbsDiff, 0.1);
    }

    [TestCase(0)]
    [TestCase(1020)]
    [TestCase(3046)]
    public void Evaluate_IsContinuous(int seed)
    {
        const float delta = 0.001f;
        for (int i = 0; i < 20000; i++)
        {
            float t = i * 0.0037f;
            float step = Math.Abs(FlameFlicker.Evaluate(t + delta, seed) - FlameFlicker.Evaluate(t, seed));
            Assert.Less(step, 0.08f, $"t={t}");
        }
    }

    [TestCase(0)]
    [TestCase(1020)]
    [TestCase(3046)]
    public void Evaluate_IsNotFlat(int seed)
    {
        const int count = 6000;
        double sum = 0, sumSq = 0;
        for (int i = 0; i < count; i++)
        {
            double v = FlameFlicker.Evaluate(i * 30f / count, seed);
            sum += v;
            sumSq += v * v;
        }
        double mean = sum / count;
        double stdDev = Math.Sqrt(Math.Max(0, sumSq / count - mean * mean));
        Assert.Greater(stdDev, 0.15);
    }

    [Test]
    public void Evaluate_NeverReturnsNaN()
    {
        float[] times = { 0f, -0.5f, -1234.5f, 1e-7f, 3600f, 86400f, 1e6f };
        foreach (float t in times)
        {
            float value = FlameFlicker.Evaluate(t, 42);
            Assert.IsFalse(float.IsNaN(value), $"t={t}");
            Assert.GreaterOrEqual(value, -1f, $"t={t}");
            Assert.LessOrEqual(value, 1f, $"t={t}");
        }
    }

    [Test]
    public void Evaluate_NonFiniteTime_ReturnsZero()
    {
        Assert.AreEqual(0f, FlameFlicker.Evaluate(float.NaN, 5));
        Assert.AreEqual(0f, FlameFlicker.Evaluate(float.PositiveInfinity, 5));
        Assert.AreEqual(0f, FlameFlicker.Evaluate(float.NegativeInfinity, 5));
    }
}
