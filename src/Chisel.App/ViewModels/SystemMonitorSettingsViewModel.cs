using System;
using Chisel.Core.SystemMonitor;

namespace Chisel.App.ViewModels;

// The System monitor page in the Widgets settings: which stats the widget shows and how often it samples (1, 2 or 5 seconds).
// Each change is saved at once.
public sealed class SystemMonitorSettingsViewModel : ViewModelBase
{
    private readonly ISystemMonitorSettingsRepository? _repository;
    private SystemStats _shown;
    private int _refreshSeconds;

    public SystemMonitorSettingsViewModel(ISystemMonitorSettingsRepository? repository)
    {
        _repository = repository;
        var saved = (repository?.Get() ?? SystemMonitorSettings.Default).Normalized();
        _shown = saved.Shown;
        _refreshSeconds = saved.RefreshSeconds;
    }

    public SystemStats Shown => _shown;

    public int RefreshSeconds => _refreshSeconds;

    public TimeSpan Interval => TimeSpan.FromSeconds(_refreshSeconds);

    public bool ShowCpu { get => Get(SystemStat.Cpu); set => Set(SystemStat.Cpu, value); }
    public bool ShowRam { get => Get(SystemStat.Ram); set => Set(SystemStat.Ram, value); }
    public bool ShowDisk { get => Get(SystemStat.Disk); set => Set(SystemStat.Disk, value); }
    public bool ShowNetwork { get => Get(SystemStat.Network); set => Set(SystemStat.Network, value); }
    public bool ShowBattery { get => Get(SystemStat.Battery); set => Set(SystemStat.Battery, value); }

    public bool IsRefresh1 { get => _refreshSeconds == 1; set => Choose(1, value); }
    public bool IsRefresh2 { get => _refreshSeconds == 2; set => Choose(2, value); }
    public bool IsRefresh5 { get => _refreshSeconds == 5; set => Choose(5, value); }

    private bool Get(SystemStat stat) => _shown.Includes(stat);

    private void Set(SystemStat stat, bool on)
    {
        var shown = _shown.With(stat, on);
        if (shown == _shown)
        {
            return;
        }
        _shown = shown;
        OnPropertyChanged("Show" + stat);
        OnPropertyChanged(nameof(Shown));
        Save();
    }

    private void Choose(int seconds, bool on)
    {
        if (!on || seconds == _refreshSeconds)
        {
            return;
        }
        _refreshSeconds = seconds;
        OnPropertyChanged(nameof(IsRefresh1));
        OnPropertyChanged(nameof(IsRefresh2));
        OnPropertyChanged(nameof(IsRefresh5));
        OnPropertyChanged(nameof(RefreshSeconds));
        OnPropertyChanged(nameof(Interval));
        Save();
    }

    private void Save() => _repository?.Save(new SystemMonitorSettings(_shown, _refreshSeconds));
}

// The system monitor's shared state: its settings and the one sampler all its views watch, kept at the chosen rate.
public sealed class SystemMonitorViewModel : IDisposable
{
    public SystemMonitorViewModel(SystemMonitorSettingsViewModel settings, ISystemStatsSource source, TimeProvider time, Action<Action>? dispatch = null)
    {
        Settings = settings;
        Sampler = new SystemMonitorSampler(source, time, dispatch) { Interval = settings.Interval };
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemMonitorSettingsViewModel.Interval))
            {
                Sampler.Interval = settings.Interval;
            }
        };
    }

    public SystemMonitorSettingsViewModel Settings { get; }

    public SystemMonitorSampler Sampler { get; }

    public void Dispose() => Sampler.Dispose();
}
