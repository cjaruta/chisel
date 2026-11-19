using Chisel.Core.Settings;

namespace Chisel.Core.Tests.Settings;

public class MediaGradientSettingsTests
{
    private static readonly UserSettings Defaults = new("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));

    [Fact]
    public void AlbumGradientAndAnimation_AreOnByDefault()
    {
        Assert.True(Defaults.MediaAlbumGradient);
        Assert.True(Defaults.MediaAnimateGradient);
    }

    [Fact]
    public void WithMethods_ChangeOnlyTheirOwnSetting()
    {
        var changed = Defaults.WithMediaVolume(false).WithMediaAlbumGradient(false).WithMediaAnimateGradient(false);
        Assert.False(changed.MediaAlbumGradient);
        Assert.False(changed.MediaAnimateGradient);
        Assert.False(changed.MediaVolume);
        Assert.True(changed.WithMediaAlbumGradient(true).WithMediaVisualizer(false).MediaAlbumGradient);
        Assert.False(changed.WithMediaAlbumGradient(true).MediaAnimateGradient);
    }
}
