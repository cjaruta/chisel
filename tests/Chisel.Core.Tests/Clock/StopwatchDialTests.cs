using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

public class StopwatchDialTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(15, 90, 3)] // a quarter of the way round the seconds dial
    [InlineData(30, 180, 6)] // half a minute: the minutes hand has moved a twelfth of a half-hour turn
    [InlineData(60, 0, 12)] // a whole minute: the seconds hand is back at the top
    [InlineData(75.5, 93, 15.1)] // the hand sweeps: it is between two marks
    [InlineData(1800, 0, 0)] // half an hour: the minutes hand is back at the top
    [InlineData(1799.5, 357, 359.9)]
    public void TheHands_FollowTheElapsedTime_SweepingWithoutSteps(double seconds, double secondsAngle, double minutesAngle)
    {
        var angles = StopwatchDial.Angles(seconds);

        Assert.Equal(secondsAngle, angles.Seconds, 6);
        Assert.Equal(minutesAngle, angles.Minutes, 6);
    }

    [Fact]
    public void ANegativeOrHugeTime_StaysOnTheDial()
    {
        Assert.Equal(0, StopwatchDial.Angles(-5).Seconds);
        var big = StopwatchDial.Angles(100 * 3600 + 12.25);
        Assert.InRange(big.Seconds, 0, 360);
        Assert.InRange(big.Minutes, 0, 360);
    }
}
