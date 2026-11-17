using Chisel.App.ViewModels;

namespace Chisel.App.Tests.ViewModels;

// Press and current pointer positions on the day grid to a normalized, snapped start and end.
// Positions are in pixels from where midnight would be with no scrolling (1 px per minute here).
public class DragRangeTests
{
    private static (TimeOnly Start, TimeOnly End) Range(double pressY, double currentY, double scrollOffset = 0)
    {
        var range = ScheduleGridLayout.DragRange(pressY, currentY, scrollOffset);
        return (range.Start, range.End);
    }

    private static (TimeOnly, TimeOnly) Times(int startHour, int startMinute, int endHour, int endMinute) =>
        (new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

    [Fact]
    public void DragDown_NoonToQuarterPastThree_IsNoonToQuarterPastThree()
    {
        Assert.Equal(Times(12, 0, 15, 15), Range(720, 915));
    }

    [Fact]
    public void DragUp_TheEarlierTimeIsTheStart()
    {
        Assert.Equal(Times(12, 0, 15, 15), Range(915, 720));
    }

    [Fact]
    public void CurrentPosition_SevenMinutesPastAMark_SnapsDown()
    {
        Assert.Equal(Times(12, 0, 15, 15), Range(720, 922));
    }

    [Fact]
    public void CurrentPosition_EightMinutesPastAMark_SnapsUp()
    {
        Assert.Equal(Times(12, 0, 15, 30), Range(720, 923));
    }

    [Fact]
    public void PressPosition_SnapsToo()
    {
        Assert.Equal(Times(12, 15, 15, 0), Range(728, 900));
    }

    [Fact]
    public void DragAboveTheTop_StopsAtMidnight()
    {
        Assert.Equal(Times(0, 0, 1, 0), Range(60, -200));
    }

    [Fact]
    public void DragBelowTheBottom_EndsAtMidnightAsZeroHundred()
    {
        var range = ScheduleGridLayout.DragRange(1380, 2000, 0);

        Assert.Equal(1380, range.StartMinute);
        Assert.Equal(1440, range.EndMinute);
        Assert.Equal(Times(23, 0, 0, 0), (range.Start, range.End));
    }

    [Fact]
    public void PressAtTheVeryBottom_StillGivesOneInterval()
    {
        Assert.Equal(Times(23, 45, 0, 0), Range(1440, 1440));
    }

    [Theory]
    [InlineData(720, 720)]
    [InlineData(720, 725)]
    [InlineData(720, 714)]
    public void SameSnappedTime_IsOneIntervalLong(double pressY, double currentY)
    {
        Assert.Equal(Times(12, 0, 12, 15), Range(pressY, currentY));
    }

    [Fact]
    public void DragUpToTheTopFromMidnight_IsOneIntervalLong()
    {
        Assert.Equal(Times(0, 0, 0, 15), Range(0, -100));
    }

    [Fact]
    public void ScrollOffset_IsAddedToBothPositions()
    {
        // Scrolled 620 px: 100 px down is 12:00 and 295 px down is 15:15.
        Assert.Equal(Times(12, 0, 15, 15), Range(100, 295, scrollOffset: 620));
    }
}
