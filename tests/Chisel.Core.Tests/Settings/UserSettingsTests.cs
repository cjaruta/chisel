using Chisel.Core.Settings;
using Xunit;

namespace Chisel.Core.Tests.Settings;

public class UserSettingsTests
{
    [Fact]
    public void Constructor_ValidValues_CreatesSuccessfully()
    {
        // Arrange
        var settings = new UserSettings("Caden", new TimeOnly(6, 30), new TimeOnly(23, 0));

        // Act
        var name = settings.Name;

        // Assert
        Assert.Equal("Caden", name);
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsArgumentException()
    {
        // Arrange
        void Act() => new UserSettings("   ", new TimeOnly(6, 30), new TimeOnly(23, 0));

        // Act & Assert
        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void Constructor_WakeTimeEqualsBedTime_ThrowsArgumentException()
    {
        // Arrange
        void Act() => new UserSettings("Caden", new TimeOnly(6, 30), new TimeOnly(6, 30));

        // Act & Assert
        Assert.Throws<ArgumentException>(Act);
    }
}