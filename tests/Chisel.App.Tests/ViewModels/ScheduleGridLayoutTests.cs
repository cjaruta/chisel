using Chisel.App.ViewModels;
using Chisel.Core.Schedule;

namespace Chisel.App.Tests.ViewModels;

public class ScheduleGridLayoutTests
{
    private static ScheduleBlock Block(string title, int startHour, int startMinute, int endHour, int endMinute) =>
        new(title, new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

    [Fact]
    public void Segments_SameDayBlock_IsOneSegmentInMinutesFromMidnight()
    {
        var segments = ScheduleGridLayout.Segments(new[] { Block("Focus", 8, 0, 11, 30) });

        var segment = Assert.Single(segments);
        Assert.Equal(0, segment.BlockIndex);
        Assert.Equal(480, segment.StartMinute);
        Assert.Equal(690, segment.EndMinute);
        Assert.Equal(0, segment.Column);
        Assert.Equal(1, segment.ColumnCount);
    }

    [Fact]
    public void Segments_OvernightBlock_SplitsIntoEarlyMorningAndLateNightPieces()
    {
        var segments = ScheduleGridLayout.Segments(new[] { Block("Sleep", 23, 0, 6, 30) });

        Assert.Equal(2, segments.Count);
        Assert.Equal((0, 390), (segments[0].StartMinute, segments[0].EndMinute));
        Assert.Equal((1380, 1440), (segments[1].StartMinute, segments[1].EndMinute));
        Assert.All(segments, s => Assert.Equal(0, s.BlockIndex));
    }

    [Fact]
    public void Segments_BlockEndingAtMidnight_IsOneSegmentToTheEndOfTheDay()
    {
        var segment = Assert.Single(ScheduleGridLayout.Segments(new[] { Block("Read", 22, 0, 0, 0) }));

        Assert.Equal((1320, 1440), (segment.StartMinute, segment.EndMinute));
    }

    [Fact]
    public void Segments_NonOverlappingBlocks_EachTakeTheFullWidth()
    {
        var segments = ScheduleGridLayout.Segments(new[]
        {
            Block("Focus", 8, 0, 11, 0),
            Block("Lunch", 11, 0, 12, 0), // touching is not overlapping
        });

        Assert.All(segments, s => Assert.Equal((0, 1), (s.Column, s.ColumnCount)));
    }

    [Fact]
    public void Segments_OverlappingBlocks_SitSideBySide()
    {
        var segments = ScheduleGridLayout.Segments(new[]
        {
            Block("Call", 9, 0, 10, 0),
            Block("Focus", 8, 0, 11, 0),
        });

        var focus = segments.Single(s => s.BlockIndex == 1);
        var call = segments.Single(s => s.BlockIndex == 0);
        Assert.Equal((0, 2), (focus.Column, focus.ColumnCount));
        Assert.Equal((1, 2), (call.Column, call.ColumnCount));
    }

    [Fact]
    public void Segments_ReuseAFreedColumnWithinTheSameOverlapGroup()
    {
        // Focus overlaps both A and B, but A ends before B starts, so B goes back into A's column.
        var segments = ScheduleGridLayout.Segments(new[]
        {
            Block("Focus", 8, 0, 12, 0),
            Block("A", 8, 30, 9, 30),
            Block("B", 10, 0, 11, 0),
        });

        Assert.Equal(1, segments.Single(s => s.BlockIndex == 1).Column);
        Assert.Equal(1, segments.Single(s => s.BlockIndex == 2).Column);
        Assert.All(segments, s => Assert.Equal(2, s.ColumnCount));
    }

    [Fact]
    public void Segments_SeparateOverlapGroups_GetTheirOwnColumnCounts()
    {
        var segments = ScheduleGridLayout.Segments(new[]
        {
            Block("Focus", 8, 0, 10, 0),
            Block("Call", 9, 0, 10, 0),
            Block("Workout", 14, 0, 15, 0),
        });

        Assert.Equal(1, segments.Single(s => s.BlockIndex == 2).ColumnCount);
        Assert.Equal(2, segments.Single(s => s.BlockIndex == 0).ColumnCount);
    }

    [Fact]
    public void Segments_OvernightPieceOverlapsAnEarlyMorningBlock()
    {
        var segments = ScheduleGridLayout.Segments(new[]
        {
            Block("Sleep", 23, 0, 6, 30),
            Block("Run", 6, 0, 7, 0),
        });

        var morningSleep = segments.Single(s => s.BlockIndex == 0 && s.StartMinute == 0);
        var lateSleep = segments.Single(s => s.BlockIndex == 0 && s.StartMinute == 1380);
        var run = segments.Single(s => s.BlockIndex == 1);
        Assert.Equal((0, 2), (morningSleep.Column, morningSleep.ColumnCount));
        Assert.Equal((1, 2), (run.Column, run.ColumnCount));
        Assert.Equal((0, 1), (lateSleep.Column, lateSleep.ColumnCount));
    }

    [Theory]
    [InlineData(0, 0, 0, 0.0)]
    [InlineData(10, 30, 0, 630.0)]
    [InlineData(10, 30, 30, 630.5)]
    [InlineData(23, 59, 0, 1439.0)]
    public void MinuteOfDay_CountsMinutesAndSecondsFromMidnight(int hour, int minute, int second, double expected)
    {
        Assert.Equal(expected, ScheduleGridLayout.MinuteOfDay(new TimeOnly(hour, minute, second)));
    }
}
