using Chisel.Core.Pictures;
using Chisel.Core.Settings;

namespace Chisel.Core.Tests.Pictures;

public class PictureSettingsTests
{
    private static UserSettings Settings() => new("Test User", new TimeOnly(7, 0), new TimeOnly(23, 0));

    [Fact]
    public void Defaults_KeepOriginals_AndMakeOneSlideshow()
    {
        Assert.True(Settings().PictureKeepOriginals);
        Assert.Equal(MultiDropMode.OneSlideshow, Settings().PictureMultiDrop);
    }

    [Fact]
    public void TheTwoOptions_ChangeAlone_AndSurviveOtherChanges()
    {
        var changed = Settings().WithPictureKeepOriginals(false).WithPictureMultiDrop(MultiDropMode.OneFramePerPicture);

        Assert.False(changed.PictureKeepOriginals);
        Assert.Equal(MultiDropMode.OneFramePerPicture, changed.PictureMultiDrop);
        var later = changed.WithThemeMode("light").WithClockFormat(ClockFormat.TwelveHour).WithMediaVolume(false);
        Assert.False(later.PictureKeepOriginals);
        Assert.Equal(MultiDropMode.OneFramePerPicture, later.PictureMultiDrop);
        Assert.Equal("Test User", later.Name);
    }
}
