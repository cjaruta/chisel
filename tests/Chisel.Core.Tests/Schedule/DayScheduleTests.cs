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
}