using Chisel.Core.Media;

namespace Chisel.Core.Tests.Media;

public class SpectrumProcessorTests
{
    private const int Rate = 48000;

    private static float[] Sine(double hz, int count = 4096, double amplitude = 0.5)
    {
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * hz * i / Rate));
        }
        return samples;
    }

    private static float[] Levels(SpectrumProcessor p)
    {
        var levels = new float[p.BandCount];
        p.CopyLevels(levels);
        return levels;
    }

    private static int Loudest(float[] levels) => Array.IndexOf(levels, levels.Max());

    [Fact]
    public void Silence_GivesZeroInEveryBand()
    {
        var p = new SpectrumProcessor(Rate);
        p.Push(new float[4096]);
        p.Update(0.033);
        Assert.All(Levels(p), l => Assert.Equal(0f, l));
    }

    [Fact]
    public void LowTone_LightsLowBandsNotHighOnes()
    {
        var p = new SpectrumProcessor(Rate);
        p.Push(Sine(100));
        p.Update(1);
        var levels = Levels(p);
        Assert.True(Loudest(levels) < levels.Length / 4);
        Assert.Equal(0f, levels[^1]);
    }

    [Fact]
    public void HighTone_LightsHighBandsNotLowOnes()
    {
        var p = new SpectrumProcessor(Rate);
        p.Push(Sine(8000));
        p.Update(1);
        var levels = Levels(p);
        Assert.True(Loudest(levels) > levels.Length * 3 / 4);
        Assert.Equal(0f, levels[0]);
    }

    [Fact]
    public void LevelsStayBetweenZeroAndOne()
    {
        var p = new SpectrumProcessor(Rate);
        p.Push(Sine(1000, amplitude: 1));
        p.Update(1);
        Assert.All(Levels(p), l => Assert.InRange(l, 0f, 1f));
    }

    [Fact]
    public void AfterTheSoundStops_BarsFallGradually_ThenReachZero()
    {
        var p = new SpectrumProcessor(Rate);
        p.Push(Sine(1000));
        p.Update(1);
        var peak = Levels(p).Max();
        p.Clear();
        p.Update(0.033);
        var after = Levels(p).Max();
        Assert.True(after < peak && after > 0);
        for (var i = 0; i < 100; i++)
        {
            p.Update(0.033);
        }
        Assert.Equal(0f, Levels(p).Max());
    }

    [Fact]
    public void Attack_IsFasterThanDecay()
    {
        var p = new SpectrumProcessor(Rate);
        p.Push(Sine(1000));
        p.Update(0.033);
        var rise = Levels(p).Max();
        p.Update(1);
        var full = Levels(p).Max();
        p.Clear();
        p.Update(0.033);
        var fall = full - Levels(p).Max();
        Assert.True(rise > fall);
    }

    [Fact]
    public void Bands_CoverTheRange_AndRespectTheSampleRate()
    {
        var p = new SpectrumProcessor(22050, bandCount: 16);
        Assert.Equal(16, p.BandCount);
        p.Push(Sine(500, amplitude: 0.5));
        p.Update(1);
        Assert.True(Levels(p).Max() > 0);
    }
}
