using System;
using Chisel.App.ViewModels;
using Chisel.Core.Settings;

namespace Chisel.App.Tests.ViewModels;

public class MediaSettingsTests
{
    private sealed class FakeSettingsRepository(UserSettings? initial) : IUserSettingsRepository
    {
        public UserSettings? Saved { get; private set; } = initial;
        public UserSettings? Get() => Saved;
        public void Save(UserSettings settings) => Saved = settings;
    }

    private static readonly UserSettings Defaults = new("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));

    [Fact]
    public void Visualizer_IsOnByDefault_EvenWithNothingStored()
    {
        Assert.True(new MediaSettingsViewModel(new FakeSettingsRepository(Defaults)).ShowVisualizer);
        Assert.True(new MediaSettingsViewModel(null).ShowVisualizer);
    }

    [Fact]
    public void Visualizer_LoadsTheStoredChoice()
    {
        var repository = new FakeSettingsRepository(Defaults.WithMediaVisualizer(false));
        Assert.False(new MediaSettingsViewModel(repository).ShowVisualizer);
    }

    [Fact]
    public void TurningTheVisualizerOff_IsSavedAtOnce_AndKeepsOtherSettings()
    {
        var repository = new FakeSettingsRepository(Defaults.WithTemperatureUnit(TemperatureUnit.Fahrenheit));
        var settings = new MediaSettingsViewModel(repository);

        settings.ShowVisualizer = false;

        Assert.False(repository.Saved!.MediaVisualizer);
        Assert.Equal(TemperatureUnit.Fahrenheit, repository.Saved.TemperatureUnit);
    }
}
