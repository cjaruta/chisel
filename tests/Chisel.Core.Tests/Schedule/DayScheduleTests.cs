using Chisel.Core.Schedule;
using Xunit;

namespace Chisel.Core.Tests.Schedule;

public class DayScheduleTests
{
    [Fact]
    public void CurrentBlock_TimeInsideABlock_ReturnsThatBlock()
    {
        // Arrange
        var workout = new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0));
        var schedule = new DaySchedule(new[] { workout });

        // Act
        var result = schedule.CurrentBlock(new TimeOnly(14, 30));

        // Assert
        Assert.Equal(workout, result);
    }

    [Fact]
    public void CurrentBlock_TimeInAnOpenGap_ReturnsNull()
    {
        // Arrange
        var workout = new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0));
        var schedule = new DaySchedule(new[] { workout });

        // Act
        var result = schedule.CurrentBlock(new TimeOnly(16, 0));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void NextBlock_TimeBeforeABlockStarts_ReturnsTheSoonestUpcomingBlock()
    {
        // Arrange
        var workout = new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0));
        var dinner = new ScheduleBlock("Dinner", new TimeOnly(18, 30), new TimeOnly(19, 30));
        var schedule = new DaySchedule(new[] { workout, dinner });

        // Act
        var result = schedule.NextBlock(new TimeOnly(12, 0));

        // Assert
        Assert.Equal(workout, result);
    }

    [Fact]
    public void NextBlock_NoBlocksStartLaterThatDay_ReturnsNull()
    {
        // Arrange
        var workout = new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0));
        var schedule = new DaySchedule(new[] { workout });

        // Act
        var result = schedule.NextBlock(new TimeOnly(20, 0));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Gaps_TwoBlocksWithSpaceBetween_ReturnsTheOpenStretches()
    {
        // Arrange
        var workout = new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0));
        var dinner = new ScheduleBlock("Dinner", new TimeOnly(18, 30), new TimeOnly(19, 30));
        var schedule = new DaySchedule(new[] { workout, dinner });

        // Act
        var gaps = schedule.Gaps(new TimeOnly(6, 30), new TimeOnly(23, 0)).ToList();

        // Assert
        Assert.Equal(3, gaps.Count);
        Assert.Equal((new TimeOnly(6, 30), new TimeOnly(14, 0)), gaps[0]);
        Assert.Equal((new TimeOnly(15, 0), new TimeOnly(18, 30)), gaps[1]);
        Assert.Equal((new TimeOnly(19, 30), new TimeOnly(23, 0)), gaps[2]);
    }
}