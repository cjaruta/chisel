using Chisel.Core.Settings;
using Xunit;

namespace Chisel.Core.Tests.Settings;

public class UserSettingsTests
{
    [Fact]
    public void Constructor_ValidValues_CreatesSuccessfully()
    {
        // Arrange
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));

        // Act
        var name = settings.Name;

        // Assert
        Assert.Equal("Test User", name);
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
        void Act() => new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(6, 30));

        // Act & Assert
        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void SchedulePalette_WhenNotGiven_IsTheDefault()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));

        Assert.Equal(UserSettings.DefaultSchedulePalette, settings.SchedulePalette);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void SchedulePalette_Blank_IsTheDefault(string? palette)
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), schedulePalette: palette);

        Assert.Equal(UserSettings.DefaultSchedulePalette, settings.SchedulePalette);
    }

    [Fact]
    public void WithSchedulePalette_ChangesOnlyThePalette()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), ClockFormat.TwelveHour);

        var changed = settings.WithSchedulePalette("saturated");

        Assert.Equal("saturated", changed.SchedulePalette);
        Assert.Equal(("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), ClockFormat.TwelveHour),
            (changed.Name, changed.WakeTime, changed.BedTime, changed.ClockFormat));
    }

    [Fact]
    public void WithClockFormat_KeepsThePalette()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), schedulePalette: "saturated");

        Assert.Equal("saturated", settings.WithClockFormat(ClockFormat.TwelveHour).SchedulePalette);
    }
}
