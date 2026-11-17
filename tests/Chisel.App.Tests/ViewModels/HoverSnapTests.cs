using Chisel.App.ViewModels;

namespace Chisel.App.Tests.ViewModels;

// Pointer position on the day grid (plus how far it is scrolled) to the nearest guide-line time.
public class HoverSnapTests
{
    [Fact]
    public void SnapInterval_IsFifteenMinutes()
    {
        Assert.Equal(15, ScheduleGridLayout.HoverSnapMinutes);
    }

    [Fact]
    public void SnapTime_ExactlyOnAnHour_IsThatHour()
    {
        Assert.Equal(new TimeOnly(12, 0), ScheduleGridLayout.SnapTime(720, 0));
    }

    [Fact]
    public void SnapTime_SevenMinutesPastAMark_RoundsDown()
    {
        Assert.Equal(new TimeOnly(12, 0), ScheduleGridLayout.SnapTime(727, 0));
    }

    [Fact]
    public void SnapTime_EightMinutesPastAMark_RoundsUp()
    {
        Assert.Equal(new TimeOnly(12, 15), ScheduleGridLayout.SnapTime(728, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void SnapTime_AtOrAboveTheTop_IsMidnight(double y)
    {
        Assert.Equal(new TimeOnly(0, 0), ScheduleGridLayout.SnapTime(y, 0));
    }

    [Theory]
    [InlineData(1439)]
    [InlineData(1440)]
    [InlineData(2000)]
    public void SnapTime_AtOrBelowTheBottom_IsTheLastMark(double y)
    {
        Assert.Equal(new TimeOnly(23, 45), ScheduleGridLayout.SnapTime(y, 0));
    }

    [Fact]
    public void SnapTime_AddsTheScrollOffset()
    {
        // 100 px down a viewport scrolled by 628 px is minute 728 = 12:08.
        Assert.Equal(new TimeOnly(12, 15), ScheduleGridLayout.SnapTime(100, 628));
    }

    [Fact]
    public void SnapTime_UsesThePixelsPerMinuteScale()
    {
        Assert.Equal(new TimeOnly(12, 0), ScheduleGridLayout.SnapTime(1454, 0, pixelsPerMinute: 2));
    }
}
