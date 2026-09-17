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

    [Fact]
    public void CrossesMidnight_SameDayBlock_ReturnsFalse()
    {
    // Arrange
    var block = new ScheduleBlock("Focused work", new TimeOnly(8, 0), new TimeOnly(11, 0));

    // Act
    var crossesMidnight = block.CrossesMidnight;

    // Assert
    Assert.False(crossesMidnight);
    }

    [Fact]
    public void CrossesMidnight_BlockEndingNextDay_ReturnsTrue()
    {
        // Arrange
        var block = new ScheduleBlock("Sleep", new TimeOnly(23, 0), new TimeOnly(6, 30));

        // Act
        var crossesMidnight = block.CrossesMidnight;

        // Assert
        Assert.True(crossesMidnight);
    }

    [Theory]
    [InlineData(9, 0, true)]   // 09:00 is inside the block
    [InlineData(8, 0, true)]   // the start itself counts as inside
    [InlineData(11, 0, false)] // the end itself does not count as inside
    [InlineData(7, 59, false)] // just before the start
    public void Contains_SameDayBlock_ReturnsExpectedResult(int hour, int minute, bool expected)
    {
        // Arrange
        var block = new ScheduleBlock("Focused work", new TimeOnly(8, 0), new TimeOnly(11, 0));
        var timeToCheck = new TimeOnly(hour, minute);

        // Act
        var result = block.Contains(timeToCheck);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(23, 30, true)]  // shortly after start, before midnight
    [InlineData(2, 0, true)]    // after midnight, before end
    [InlineData(6, 30, false)]  // the end itself
    [InlineData(12, 0, false)]  // midday, clearly outside
    public void Contains_MidnightCrossingBlock_ReturnsExpectedResult(int hour, int minute, bool expected)
    {
        // Arrange
        var block = new ScheduleBlock("Sleep", new TimeOnly(23, 0), new TimeOnly(6, 30));
        var timeToCheck = new TimeOnly(hour, minute);

        // Act
        var result = block.Contains(timeToCheck);

        // Assert
        Assert.Equal(expected, result);
    }

}


