using System;
using Chisel.App.ViewModels;
using Chisel.Core.Settings;

namespace Chisel.App.Tests.ViewModels;

public class MediaGradientSettingsViewModelTests
{
    private sealed class FakeSettingsRepository(UserSettings? initial) : IUserSettingsRepository
    {
        public UserSettings? Saved { get; private set; } = initial;
        public UserSettings? Get() => Saved;
        public void Save(UserSettings settings) => Saved = settings;
    }

    private static readonly UserSettings Defaults = new("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));

    [Fact]
    public void BothOptions_AreOnByDefault_WithOrWithoutStoredSettings()
    {
        foreach (var settings in new[] { new MediaSettingsViewModel(null), new MediaSettingsViewModel(new FakeSettingsRepository(Defaults)) })
        {
            Assert.True(settings.AlbumGradient);
            Assert.True(settings.AnimateGradient);
        }
    }

    [Fact]
    public void BothOptions_LoadTheStoredChoices_AndSaveAtOnceKeepingOtherSettings()
    {
        var repository = new FakeSettingsRepository(Defaults.WithMediaAlbumGradient(false).WithMediaVolume(false));
        var settings = new MediaSettingsViewModel(repository);
        Assert.False(settings.AlbumGradient);
        Assert.True(settings.AnimateGradient);

        settings.AlbumGradient = true;
        settings.AnimateGradient = false;

        Assert.True(repository.Saved!.MediaAlbumGradient);
        Assert.False(repository.Saved.MediaAnimateGradient);
        Assert.False(repository.Saved.MediaVolume);
    }
}
