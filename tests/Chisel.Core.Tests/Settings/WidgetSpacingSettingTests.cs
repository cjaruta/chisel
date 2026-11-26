using Chisel.Core.Settings;
using Xunit;

namespace Chisel.Core.Tests.Settings;

// The space between widgets: 16 px unless chosen, kept between nearly none and roomy.
public class WidgetSpacingSettingTests
{
    private static UserSettings Make(int spacing = UserSettings.DefaultWidgetSpacing) =>
        new("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), widgetSpacing: spacing);

    [Fact]
    public void ItDefaultsToWhatTheAppAlwaysHad()
    {
        Assert.Equal(16, new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0)).WidgetSpacing);
    }

    [Theory]
    [InlineData(-5, 2)]
    [InlineData(0, 2)]
    [InlineData(2, 2)]
    [InlineData(20, 20)]
    [InlineData(32, 32)]
    [InlineData(400, 32)]
    public void ItStaysWithinTheLimits(int asked, int kept) => Assert.Equal(kept, Make(asked).WidgetSpacing);

    [Fact]
    public void ChangingIt_KeepsEveryOtherSetting_AndOtherChangesKeepIt()
    {
        var changed = Make().WithThemeMode("light").WithWidgetSpacing(4);

        Assert.Equal((4, "light"), (changed.WidgetSpacing, changed.ThemeMode));
        Assert.Equal(4, changed.WithClockFormat(ClockFormat.TwelveHour).WithAccentColor("#112233").WithPictureKeepOriginals(false).WidgetSpacing);
    }
}
