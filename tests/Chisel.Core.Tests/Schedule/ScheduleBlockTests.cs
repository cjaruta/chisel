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
}