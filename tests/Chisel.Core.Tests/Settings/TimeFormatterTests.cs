using Chisel.Core.Settings;

namespace Chisel.Core.Tests.Settings;

public class TimeFormatterTests
{
    [Theory]
    [InlineData(0, 0, "00:00", "12:00 AM")]
    [InlineData(0, 5, "00:05", "12:05 AM")]
    [InlineData(12, 0, "12:00", "12:00 PM")]
    [InlineData(13, 5, "13:05", "1:05 PM")]
    [InlineData(23, 59, "23:59", "11:59 PM")]
    public void Format_WithoutSeconds_InBothFormats(int hour, int minute, string expected24, string expected12)
    {
        var time = new TimeOnly(hour, minute);

        Assert.Equal(expected24, TimeFormatter.Format(time, ClockFormat.TwentyFourHour));
        Assert.Equal(expected12, TimeFormatter.Format(time, ClockFormat.TwelveHour));
    }

    [Theory]
    [InlineData(22, 36, 12, "22:36:12", "10:36:12 PM")]
    [InlineData(0, 0, 0, "00:00:00", "12:00:00 AM")]
    [InlineData(9, 5, 7, "09:05:07", "9:05:07 AM")]
    public void Format_WithSeconds_InBothFormats(int hour, int minute, int second, string expected24, string expected12)
    {
        var time = new TimeOnly(hour, minute, second);

        Assert.Equal(expected24, TimeFormatter.Format(time, ClockFormat.TwentyFourHour, withSeconds: true));
        Assert.Equal(expected12, TimeFormatter.Format(time, ClockFormat.TwelveHour, withSeconds: true));
    }

    [Fact]
    public void FormatRange_JoinsBothEnds()
    {
        Assert.Equal("23:00-06:30", TimeFormatter.FormatRange(new TimeOnly(23, 0), new TimeOnly(6, 30), ClockFormat.TwentyFourHour));
        Assert.Equal("11:00 PM-6:30 AM", TimeFormatter.FormatRange(new TimeOnly(23, 0), new TimeOnly(6, 30), ClockFormat.TwelveHour));
    }
}
