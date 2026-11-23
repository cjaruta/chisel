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

    // The stopwatch moves in steps of 0.2 s, and the step is the one the time has reached (rounded down): the display never runs
    // ahead of real time.
    [Theory]
    [InlineData(0, "00:00.0")]
    [InlineData(199, "00:00.0")]
    [InlineData(200, "00:00.2")]
    [InlineData(399, "00:00.2")]
    [InlineData(400, "00:00.4")]
    [InlineData(999, "00:00.8")]
    [InlineData(1000, "00:01.0")]
    [InlineData(1234, "00:01.2")]
    [InlineData(1999, "00:01.8")]
    [InlineData(61999, "01:01.8")]
    [InlineData(3599999, "59:59.8")]
    [InlineData(3600000, "1:00:00.0")]
    [InlineData(3600199, "1:00:00.0")]
    public void Stopwatch_ShowsStepsOfTwoTenths_RoundedDown(int milliseconds, string expected) =>
        Assert.Equal(expected, ClockToolsFormat.Stopwatch(TimeSpan.FromMilliseconds(milliseconds)));

    [Fact]
    public void Stopwatch_OnlyEverShowsEvenTenths_AndNeverRunsAhead()
    {
        for (var ms = 0; ms < 200_000; ms += 7)
        {
            var text = ClockToolsFormat.Stopwatch(TimeSpan.FromMilliseconds(ms));

            Assert.Contains(text[^1], "02468");
            Assert.Equal('.', text[^2]);
            var shown = Shown(text);
            Assert.True(shown <= ms, $"{text} is ahead of {ms} ms");
            Assert.True(ms - shown < 200, $"{text} is more than a step behind {ms} ms");
        }
    }

    [Fact]
    public void Stopwatch_OfANegativeTime_IsZero() =>
        Assert.Equal("00:00.0", ClockToolsFormat.Stopwatch(TimeSpan.FromSeconds(-3)));

    private static long Shown(string text)
    {
        var parts = text.Split('.');
        var clock = parts[0].Split(':').Select(int.Parse).ToArray();
        var seconds = clock.Length == 3 ? clock[0] * 3600 + clock[1] * 60 + clock[2] : clock[0] * 60 + clock[1];
        return seconds * 1000L + int.Parse(parts[1]) * 100;
    }
}
