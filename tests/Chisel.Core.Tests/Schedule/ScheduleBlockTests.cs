using Chisel.Core.Schedule;
using Xunit;

namespace Chisel.Core.Tests.Schedule;

public class ScheduleBlockTests
{
    [Fact]
    public void Duration_SameDayBlock_ReturnsTimeBetweenStartAndEnd()
    {
        // Arrange
        var block = new ScheduleBlock("Focused work", new TimeOnly(8, 0), new TimeOnly(11, 0));

        // Act
        var duration = block.Duration;

        // Assert
        Assert.Equal(TimeSpan.FromHours(3), duration);
    }

    [Fact]
    public void Duration_BlockCrossingMidnight_WrapsAroundToNextDay()
    {
        // Arrange
        var block = new ScheduleBlock("Sleep", new TimeOnly(23, 0), new TimeOnly(6, 30));

        // Act
        var duration = block.Duration;

        // Assert
        Assert.Equal(new TimeSpan(7, 30, 0), duration);
    }

}


