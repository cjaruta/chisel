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

    [Theory]
    [InlineData("14:30", 14, 30)]
    [InlineData("7:05", 7, 5)]
    [InlineData("07:05", 7, 5)]
    [InlineData("00:00", 0, 0)]
    [InlineData(" 23:59 ", 23, 59)]
    [InlineData("2:30 PM", 14, 30)]
    [InlineData("2:30pm", 14, 30)]
    [InlineData("12:00 AM", 0, 0)]
    [InlineData("12:15 PM", 12, 15)]
    [InlineData("11:59 PM", 23, 59)]
    [InlineData("9 AM", 9, 0)]
    [InlineData("9pm", 21, 0)]
    public void TryParse_AcceptsBothClockFormats(string text, int hour, int minute)
    {
        Assert.True(TimeFormatter.TryParse(text, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("13:00 PM")]
    [InlineData("0:30 AM")]
    [InlineData("1430")]
    [InlineData("9")]
    public void TryParse_RejectsAnythingElse(string? text)
    {
        Assert.False(TimeFormatter.TryParse(text, out _));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(13, 5)]
    [InlineData(23, 59)]
    public void TryParse_ReadsBackWhatFormatWrites(int hour, int minute)
    {
        var time = new TimeOnly(hour, minute);

        Assert.True(TimeFormatter.TryParse(TimeFormatter.Format(time, ClockFormat.TwelveHour), out var twelve));
        Assert.True(TimeFormatter.TryParse(TimeFormatter.Format(time, ClockFormat.TwentyFourHour), out var twentyFour));
        Assert.Equal(time, twelve);
        Assert.Equal(time, twentyFour);
    }
}
