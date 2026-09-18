using Chisel.Core.Schedule;
using Xunit;

namespace Chisel.Core.Tests.Schedule;

public class MarkerTests
{
    [Fact]
    public void HasPassed_TimeAfterMarker_ReturnsTrue()
    {
        // Arrange
        var marker = new Marker("Last coffee", new TimeOnly(13, 0), MarkerKind.Reminder);

        // Act
        var result = marker.HasPassed(new TimeOnly(14, 0));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasPassed_TimeBeforeMarker_ReturnsFalse()
    {
        // Arrange
        var marker = new Marker("Last coffee", new TimeOnly(13, 0), MarkerKind.Reminder);

        // Act
        var result = marker.HasPassed(new TimeOnly(12, 0));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasPassed_TimeExactlyAtMarker_ReturnsTrue()
    {
        // Arrange
        var marker = new Marker("All work done", new TimeOnly(18, 0), MarkerKind.Checkpoint);

        // Act
        var result = marker.HasPassed(new TimeOnly(18, 0));

        // Assert
        Assert.True(result);
    }
}