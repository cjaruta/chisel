using Chisel.Core.Clock;
using Xunit;

namespace Chisel.Core.Tests.Clock;

public class ClockMathTests
{
    [Fact]
    public void Midnight_GivesAllZeros()
    {
        var a = ClockMath.ClockAngles(new TimeOnly(12, 0, 0));
        Assert.Equal((0, 0, 0), (a.Hour, a.Minute, a.Second));
    }

    [Fact]
    public void ThreeOClock_PutsHourHandAt90()
    {
        var a = ClockMath.ClockAngles(new TimeOnly(3, 0, 0));
        Assert.Equal((90, 0, 0), (a.Hour, a.Minute, a.Second));
    }

    [Fact]
    public void SixThirty_PutsHourHandHalfwayBetweenSixAndSeven()
    {
        var a = ClockMath.ClockAngles(new TimeOnly(6, 30, 0));
        Assert.Equal(195, a.Hour, 6);
        Assert.Equal(180, a.Minute, 6);
    }

    [Fact]
    public void OneSecondBeforeTwelve_IsJustUnder360()
    {
        var a = ClockMath.ClockAngles(new TimeOnly(11, 59, 59));
        Assert.InRange(a.Hour, 359.99, 360);
        Assert.InRange(a.Minute, 359.9, 360);
        Assert.Equal(354, a.Second, 6);
    }

    [Fact]
    public void MinuteHandIncludesSecondFraction_AndSecondsWrap()
    {
        Assert.Equal(6.5, ClockMath.ClockAngles(new TimeOnly(1, 1, 5)).Minute, 6);
        Assert.Equal(0, ClockMath.ClockAngles(new TimeOnly(1, 1, 0)).Second, 6);
    }
}
