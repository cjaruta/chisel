using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

public class ClockToolsFormatTests
{
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(5, "00:05")]
    [InlineData(65, "01:05")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(36125, "10:02:05")]
    public void Countdown_ShowsMinutesAndSeconds_AddingHoursWhenNeeded(int seconds, string expected) =>
        Assert.Equal(expected, ClockToolsFormat.Countdown(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Countdown_RoundsPartialSecondsUp_SoItHitsZeroOnlyAtZero() =>
        Assert.Equal("00:01", ClockToolsFormat.Countdown(TimeSpan.FromMilliseconds(100)));

    [Theory]
    [InlineData(0, "00:00.0")]
    [InlineData(1234, "00:01.2")]
    [InlineData(61999, "01:01.9")]
    [InlineData(3600000, "1:00:00.0")]
    public void Stopwatch_ShowsTenthsOfASecond(int milliseconds, string expected) =>
        Assert.Equal(expected, ClockToolsFormat.Stopwatch(TimeSpan.FromMilliseconds(milliseconds)));
}
