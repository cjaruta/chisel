using System;
using System.Linq;
using Chisel.Core.Weather;
using Xunit;

namespace Chisel.Core.Tests.Weather;

public class WeatherMotionTests
{
    private static readonly MotionKind[] Moving = Enum.GetValues<MotionKind>().Where(k => k != MotionKind.None).ToArray();

    [Theory]
    [InlineData("clear-day", MotionKind.SunGlow)]
    [InlineData("clear-night", MotionKind.Stars)]
    [InlineData("partly-cloudy-day", MotionKind.Clouds)]
    [InlineData("cloudy-night", MotionKind.Clouds)]
    [InlineData("rain-day", MotionKind.Rain)]
    [InlineData("storm-night", MotionKind.HeavyRain)]
    [InlineData("snow-day", MotionKind.Snow)]
    [InlineData("fog-night", MotionKind.Fog)]
    public void Style_PicksTheSceneMotion(string key, MotionKind kind) => Assert.Equal(kind, WeatherMotion.Style(key).Kind);

    [Fact]
    public void Positions_AreDeterministic_AndInBounds()
    {
        foreach (var kind in Moving)
        {
            var count = WeatherMotion.Count(kind, 600, 240);
            for (var i = 0; i < count; i++)
            {
                for (var t = 0.0; t < 100; t += 7.3)
                {
                    var a = WeatherMotion.Get(kind, i, t, 600, 240);
                    Assert.Equal(a, WeatherMotion.Get(kind, i, t, 600, 240));
                    // Slanted streaks and rays start up to a slant's run outside the area.
                    var margin = 2 * a.Size + 0.4 * 240;
                    Assert.InRange(a.X, -margin, 600 + margin);
                    Assert.InRange(a.Y, -margin, 240 + margin);
                    Assert.InRange(a.Opacity, 0, 1);
                }
            }
        }
    }

    [Fact]
    public void Positions_LoopSeamlessly_AfterOneLoop()
    {
        foreach (var kind in Moving)
        {
            var loop = WeatherMotion.Loop(kind);
            for (var i = 0; i < WeatherMotion.Count(kind, 600, 240); i++)
            {
                var a = WeatherMotion.Get(kind, i, 3.1, 600, 240);
                var b = WeatherMotion.Get(kind, i, 3.1 + loop, 600, 240);
                Assert.Equal(a.X, b.X, 6);
                Assert.Equal(a.Y, b.Y, 6);
                Assert.Equal(a.Opacity, b.Opacity, 6);
            }
        }
    }

    [Fact]
    public void Positions_MoveSmoothly_ExceptAtTheWrap()
    {
        foreach (var kind in new[] { MotionKind.Snow, MotionKind.Stars, MotionKind.SunGlow })
        {
            for (var i = 0; i < WeatherMotion.Count(kind, 600, 240); i++)
            {
                var a = WeatherMotion.Get(kind, i, 5, 600, 240);
                var b = WeatherMotion.Get(kind, i, 5.05, 600, 240);
                if (Math.Abs(a.Y - b.Y) < 100)
                {
                    Assert.True(Math.Abs(a.X - b.X) < 5 && Math.Abs(a.Y - b.Y) < 5);
                }
            }
        }
    }

    [Fact]
    public void Counts_ScaleWithArea_AndRespectTheCap()
    {
        foreach (var kind in Moving)
        {
            var small = WeatherMotion.Count(kind, 200, 100);
            var big = WeatherMotion.Count(kind, 600, 240);
            var huge = WeatherMotion.Count(kind, 4000, 3000);
            Assert.True(small >= 1 && small <= big && big <= huge && huge <= WeatherMotion.MaxParticles);
        }
        Assert.True(WeatherMotion.Count(MotionKind.Rain, 200, 100) < WeatherMotion.Count(MotionKind.Rain, 600, 240));
        Assert.True(WeatherMotion.Count(MotionKind.HeavyRain, 4000, 3000) >= WeatherMotion.Count(MotionKind.Rain, 4000, 3000));
    }

    [Fact]
    public void Rain_HasThreeDepths_FarIsShorterAndFainterThanNear()
    {
        foreach (var kind in new[] { MotionKind.Rain, MotionKind.HeavyRain })
        {
            var far = Enumerable.Range(0, 12).Select(i => WeatherMotion.Get(kind, i, 1, 600, 240)).Where(p => p.Depth == 0).ToList();
            var near = Enumerable.Range(0, 12).Select(i => WeatherMotion.Get(kind, i, 1, 600, 240)).Where(p => p.Depth == 2).ToList();
            Assert.True(far.Count > 0 && near.Count > 0);
            Assert.True(far.Max(p => p.Size) < near.Min(p => p.Size));
            Assert.True(far.Max(p => p.Opacity) < near.Min(p => p.Opacity));
            Assert.All(far.Concat(near), p => Assert.True(p.Angle > 0));
        }
    }

    [Fact]
    public void Snow_FarFlakesAreSmallerAndFainter()
    {
        var flakes = Enumerable.Range(0, 18).Select(i => WeatherMotion.Get(MotionKind.Snow, i, 1, 600, 240)).ToList();
        Assert.True(flakes.Where(p => p.Depth == 0).Max(p => p.Size) < flakes.Where(p => p.Depth == 2).Min(p => p.Size));
        Assert.True(flakes.Where(p => p.Depth == 0).Max(p => p.Opacity) < flakes.Where(p => p.Depth == 2).Min(p => p.Opacity));
    }

    [Fact]
    public void Clouds_AreBlobsOfThreeToFivePuffs_InTwoLayersAtDifferentSpeeds()
    {
        for (var i = 0; i < 4; i++)
        {
            Assert.InRange(WeatherMotion.PuffCount(i), 3, 5);
        }
        double Travelled(int index) => Math.Abs(WeatherMotion.Get(MotionKind.Clouds, index, 10, 600, 240).X - WeatherMotion.Get(MotionKind.Clouds, index, 0, 600, 240).X);
        Assert.NotEqual(WeatherMotion.Get(MotionKind.Clouds, 0, 0, 600, 240).Depth, WeatherMotion.Get(MotionKind.Clouds, 1, 0, 600, 240).Depth);
        Assert.True(Travelled(1) > Travelled(0));
    }

    [Fact]
    public void Sun_IsAGlowPlusSixRays_ThatTurnSlowly()
    {
        Assert.Equal(7, WeatherMotion.Count(MotionKind.SunGlow, 600, 240));
        Assert.Equal(0, WeatherMotion.Get(MotionKind.SunGlow, 0, 0, 600, 240).Depth);
        var a = WeatherMotion.Get(MotionKind.SunGlow, 1, 0, 600, 240).Angle;
        var b = WeatherMotion.Get(MotionKind.SunGlow, 1, 10, 600, 240).Angle;
        Assert.InRange(b - a, 0.01, 0.5);
    }

    [Fact]
    public void Clock_NeverGoesBackwards()
    {
        var a = WeatherMotion.Now;
        Assert.True(WeatherMotion.Now >= a);
    }
}
