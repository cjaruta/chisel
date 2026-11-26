using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.Core.SystemMonitor;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class SystemMonitorViewModelTests
{
    private sealed class FakeRepository(SystemMonitorSettings? initial = null) : ISystemMonitorSettingsRepository
    {
        public SystemMonitorSettings? Saved { get; private set; } = initial;
        public int Saves { get; private set; }

        public SystemMonitorSettings Get() => Saved ?? SystemMonitorSettings.Default;

        public void Save(SystemMonitorSettings settings)
        {
            Saved = settings;
            Saves++;
        }
    }

    [Fact]
    public void Settings_LoadTheSavedChoices_WithoutSavingAgain()
    {
        var repository = new FakeRepository(new SystemMonitorSettings(SystemStats.Cpu | SystemStats.Disk, 2));
        var settings = new SystemMonitorSettingsViewModel(repository);

        Assert.True(settings.ShowCpu);
        Assert.False(settings.ShowRam);
        Assert.True(settings.ShowDisk);
        Assert.False(settings.ShowNetwork);
        Assert.False(settings.ShowBattery);
        Assert.True(settings.IsRefresh2);
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public void Settings_SaveEachChange()
    {
        var repository = new FakeRepository();
        var settings = new SystemMonitorSettingsViewModel(repository);

        settings.ShowNetwork = false;
        Assert.Equal(SystemStats.All & ~SystemStats.Network, repository.Saved!.Shown);

        settings.IsRefresh5 = true;
        Assert.Equal(5, repository.Saved!.RefreshSeconds);
        Assert.False(settings.IsRefresh1);
        Assert.Equal(2, repository.Saves);
    }

    [Fact]
    public void Monitor_SamplesAtTheChosenRate()
    {
        var time = new FakeTimeProvider();
        var settings = new SystemMonitorSettingsViewModel(new FakeRepository());
        using var monitor = new SystemMonitorViewModel(settings, new FakeSystemStats(), time);
        Assert.Equal(TimeSpan.FromSeconds(1), monitor.Sampler.Interval);

        settings.IsRefresh2 = true;

        Assert.Equal(TimeSpan.FromSeconds(2), monitor.Sampler.Interval);
    }
}
