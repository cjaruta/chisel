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

    [Fact]
    public void TemperatureUnit_DefaultsToCelsius_AndWithTemperatureUnitChangesOnlyIt()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), ClockFormat.TwelveHour, "saturated");

        var changed = settings.WithTemperatureUnit(TemperatureUnit.Fahrenheit);

        Assert.Equal(TemperatureUnit.Celsius, settings.TemperatureUnit);
        Assert.Equal((TemperatureUnit.Fahrenheit, ClockFormat.TwelveHour, "saturated"),
            (changed.TemperatureUnit, changed.ClockFormat, changed.SchedulePalette));
    }

    [Fact]
    public void MediaVisualizer_DefaultsToOn_AndWithMediaVisualizerChangesOnlyIt()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), ClockFormat.TwelveHour, "saturated");

        var changed = settings.WithMediaVisualizer(false);

        Assert.True(settings.MediaVisualizer);
        Assert.Equal((false, ClockFormat.TwelveHour, "saturated"), (changed.MediaVisualizer, changed.ClockFormat, changed.SchedulePalette));
    }

    [Fact]
    public void MediaVolume_DefaultsToOn_AndSurvivesTheOtherWithMethods()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0)).WithMediaVolume(false);

        Assert.True(new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0)).MediaVolume);
        Assert.False(settings.WithSchedulePalette("saturated").WithMediaVisualizer(false).MediaVolume);
    }

    [Fact]
    public void MediaScrollVolume_DefaultsToOff_AndSurvivesTheOtherWithMethods()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0)).WithMediaScrollVolume(true);

        Assert.False(new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0)).MediaScrollVolume);
        Assert.True(settings.WithSchedulePalette("saturated").WithMediaVolume(false).MediaScrollVolume);
    }

    [Fact]
    public void Theme_DefaultsToDefaultModeAndNoCustomAccent()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));

        Assert.Equal("default", settings.ThemeMode);
        Assert.Null(settings.AccentColor);
    }

    [Fact]
    public void WithThemeAndAccent_KeepEverythingElse()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), ClockFormat.TwelveHour, "bold")
            .WithThemeMode("ocean").WithAccentColor("#1d4ed8");

        Assert.Equal("ocean", settings.ThemeMode);
        Assert.Equal("#1D4ED8", settings.AccentColor);
        Assert.Equal(ClockFormat.TwelveHour, settings.ClockFormat);
        Assert.Equal("bold", settings.SchedulePalette);
        Assert.Equal("ocean", settings.WithClockFormat(ClockFormat.TwentyFourHour).ThemeMode);
        Assert.Equal("#1D4ED8", settings.WithMediaVolume(false).AccentColor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a color")]
    public void AccentColor_IgnoresMissingOrInvalidValues(string? value) =>
        Assert.Null(new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0)).WithAccentColor(value).AccentColor);
}
