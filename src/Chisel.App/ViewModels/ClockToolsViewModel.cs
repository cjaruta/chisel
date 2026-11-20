using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// One strip button of the clock panel.
public sealed record ModeTab(ClockMode Mode, string Title, bool IsSelected);

// The clock widget's panel: a mode strip (Clock / Alarm / Timer / Stopwatch) over the tool shown, plus the small chip that
// stands in for a running tool while the panel is collapsed. The timer and stopwatch are Core state (absolute timestamps); this
// only reads them against the TimeProvider on Tick (about every 200 ms), so a restart or a sleep needs no special handling.
// State is saved when it changes, never on Tick.
public partial class ClockToolsViewModel : ViewModelBase
{
    private readonly IClockToolsRepository? _repository;
    private readonly TimeProvider _time;
    private TimerState _timer;
    private StopwatchState _stopwatch;
    private bool _announced;

    // Raised once when the running timer reaches zero (not for one that finished while the app was closed).
    public event Action? TimerFinished;

    [ObservableProperty]
    public partial ClockToolsConfig Config { get; private set; }

    [ObservableProperty]
    public partial ClockMode Mode { get; private set; }

    // Whether the panel (mode strip and tool) is open over the clock; collapsed shows only the clock and the chip.
    [ObservableProperty]
    public partial bool IsExpanded { get; private set; }

    [ObservableProperty]
    public partial string TimerText { get; private set; } = "00:00";

    [ObservableProperty]
    public partial TimerStatus TimerStatus { get; private set; }

    [ObservableProperty]
    public partial string StopwatchText { get; private set; } = "00:00.0";

    [ObservableProperty]
    public partial bool StopwatchRunning { get; private set; }

    [ObservableProperty]
    public partial string ChipText { get; private set; } = "";

    [ObservableProperty]
    public partial bool ShowChip { get; private set; }

    public ObservableCollection<string> LapTexts { get; } = [];

    public IReadOnlyList<ClockMode> EnabledModes => Config.EnabledModes;

    public IReadOnlyList<ModeTab> Tabs => EnabledModes.Select(m => new ModeTab(m, m.ToString(), m == Mode)).ToList();

    public bool StripAtBottom => Config.Strip == StripPlacement.Bottom;

    public string TimerButtonText => TimerStatus switch
    {
        TimerStatus.Running => "Pause",
        TimerStatus.Paused => "Resume",
        TimerStatus.Finished => "Restart",
        _ => "Start",
    };

    public string StopwatchButtonText => StopwatchRunning ? "Pause" : _stopwatch.Banked > TimeSpan.Zero ? "Resume" : "Start";

    // The timer's length can only change while it is idle.
    public bool CanSetTimer => TimerStatus == TimerStatus.Idle;

    public ClockToolsViewModel(IClockToolsRepository? repository, TimeProvider time)
    {
        _repository = repository;
        _time = time;
        var state = repository?.Get() ?? new ClockToolsState(ClockToolsConfig.Default, TimerState.Idle, StopwatchState.Idle, ClockMode.Clock);
        Config = state.Config;
        _timer = state.Timer;
        _stopwatch = state.Stopwatch;
        Mode = Config.IsEnabled(state.Mode) ? state.Mode : ClockMode.Clock;
        _announced = _timer.Status(_time.GetUtcNow()) == TimerStatus.Finished;
        Refresh(false);
    }

    public void Tick() => Refresh(true);

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
        Refresh(false);
    }

    [RelayCommand]
    private void SelectMode(ClockMode mode)
    {
        if (!Config.IsEnabled(mode))
        {
            return;
        }
        Mode = mode;
        OnPropertyChanged(nameof(Tabs));
        Save();
    }

    [RelayCommand]
    private void AddTimerSeconds(int seconds)
    {
        _timer = _timer.WithDuration(_timer.Duration + TimeSpan.FromSeconds(seconds));
        Changed();
    }

    [RelayCommand]
    private void ClearTimer()
    {
        _timer = _timer.WithDuration(TimeSpan.Zero);
        Changed();
    }

    [RelayCommand]
    private void TimerStartPause()
    {
        var now = _time.GetUtcNow();
        _timer = _timer.Status(now) == TimerStatus.Running ? _timer.Pause(now) : _timer.Start(now);
        Changed();
    }

    [RelayCommand]
    private void TimerReset()
    {
        _timer = _timer.Reset();
        Changed();
    }

    [RelayCommand]
    private void StopwatchStartPause()
    {
        var now = _time.GetUtcNow();
        _stopwatch = _stopwatch.IsRunning ? _stopwatch.Pause(now) : _stopwatch.Start(now);
        Changed();
    }

    [RelayCommand]
    private void StopwatchLap()
    {
        _stopwatch = _stopwatch.Lap(_time.GetUtcNow());
        Changed();
    }

    [RelayCommand]
    private void StopwatchReset()
    {
        _stopwatch = _stopwatch.Reset();
        Changed();
    }

    // Edit mode: switch a tool on or off. Switching one off stops and clears it.
    public void SetToolEnabled(ClockMode mode, bool enabled)
    {
        if (mode == ClockMode.Clock)
        {
            return;
        }
        Config = mode switch
        {
            ClockMode.Alarm => Config with { AlarmEnabled = enabled },
            ClockMode.Timer => Config with { TimerEnabled = enabled },
            _ => Config with { StopwatchEnabled = enabled },
        };
        if (!enabled)
        {
            if (mode == ClockMode.Timer)
            {
                _timer = _timer.Reset().WithDuration(TimeSpan.Zero);
            }
            else if (mode == ClockMode.Stopwatch)
            {
                _stopwatch = StopwatchState.Idle;
            }
            if (Mode == mode)
            {
                Mode = ClockMode.Clock;
            }
        }
        OnPropertyChanged(nameof(EnabledModes));
        OnPropertyChanged(nameof(Tabs));
        Changed();
    }

    // Edit mode: put the mode strip above or below the tool.
    public void SetStrip(StripPlacement strip)
    {
        Config = Config with { Strip = strip };
        OnPropertyChanged(nameof(StripAtBottom));
        Save();
    }

    private void Changed()
    {
        Refresh(false);
        Save();
    }

    private void Save() => _repository?.Save(new ClockToolsState(Config, _timer, _stopwatch, Mode));

    private void Refresh(bool mayAlert)
    {
        var now = _time.GetUtcNow();
        var status = _timer.Status(now);
        TimerText = ClockToolsFormat.Countdown(status == TimerStatus.Idle ? _timer.Duration : _timer.Remaining(now));
        TimerStatus = status;
        if (status != TimerStatus.Finished)
        {
            _announced = false;
        }
        else if (mayAlert && !_announced)
        {
            _announced = true;
            TimerFinished?.Invoke();
        }

        var elapsed = _stopwatch.Elapsed(now);
        StopwatchText = ClockToolsFormat.Stopwatch(elapsed);
        StopwatchRunning = _stopwatch.IsRunning;
        SyncLaps();

        var timerShown = status is TimerStatus.Running or TimerStatus.Finished;
        ShowChip = !IsExpanded && (timerShown || StopwatchRunning);
        ChipText = timerShown ? (status == TimerStatus.Finished ? "Done" : TimerText) : StopwatchText[..^2];
        OnPropertyChanged(nameof(TimerButtonText));
        OnPropertyChanged(nameof(StopwatchButtonText));
        OnPropertyChanged(nameof(CanSetTimer));
    }

    // Newest lap first: "Lap n   split   total".
    private void SyncLaps()
    {
        if (LapTexts.Count == _stopwatch.Laps.Count)
        {
            return;
        }
        LapTexts.Clear();
        var splits = _stopwatch.Splits();
        for (var i = _stopwatch.Laps.Count - 1; i >= 0; i--)
        {
            LapTexts.Add($"Lap {i + 1}   {ClockToolsFormat.Stopwatch(splits[i])}   {ClockToolsFormat.Stopwatch(_stopwatch.Laps[i])}");
        }
    }
}
