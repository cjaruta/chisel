using System;
using System.Collections.ObjectModel;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// Whether a tool is doing something worth showing while it is off screen, and the short text for it (its time).
public readonly record struct ToolIndicator(bool Active, string Text);

// The timer and stopwatch, and which slots the clock widget has. The timer and stopwatch are Core state (absolute timestamps); this
// only reads them against the TimeProvider on Tick (about every 200 ms, every 100 ms while the stopwatch runs), so a restart or a sleep needs no special handling.
// State is saved when it changes, never on Tick.
public partial class ClockToolsViewModel : ViewModelBase
{
    private readonly IClockToolsRepository? _repository;
    private readonly TimeProvider _time;
    private TimerState _timer;
    private StopwatchState _stopwatch;
    // The running stopwatch is timed with the monotonic clock (a wall-clock change cannot move it): the elapsed time when it was last
    // anchored, and the monotonic timestamp of that moment. The saved state keeps wall-clock times so it survives a restart; the
    // anchor is taken again from them whenever the state is loaded or the stopwatch starts.
    private TimeSpan _anchorElapsed;
    private long _anchorStamp;
    private bool _announced;
    private (string, string, bool) _buttons;

    // Raised once when the running timer reaches zero (not for one that finished while the app was closed).
    public event Action? TimerFinished;

    // The slots of the clock widget, set on the Clock page in settings.
    [ObservableProperty]
    public partial ClockSlotsConfig Slots { get; private set; }

    [ObservableProperty]
    public partial string TimerText { get; private set; } = "00:00";

    [ObservableProperty]
    public partial TimerStatus TimerStatus { get; private set; }

    [ObservableProperty]
    public partial string StopwatchText { get; private set; } = "00:00.0";

    [ObservableProperty]
    public partial bool StopwatchRunning { get; private set; }

    public ObservableCollection<string> LapTexts { get; } = [];

    public string TimerButtonText => TimerStatus switch
    {
        TimerStatus.Running => "Pause",
        TimerStatus.Paused => "Resume",
        TimerStatus.Finished => "Restart",
        _ => "Start",
    };

    // The stopwatch's elapsed time in seconds right now, for the analog face to read at every frame (no allocation).
    public double StopwatchElapsedSeconds() => StopwatchElapsed().TotalSeconds;

    // The elapsed time now: what was banked, or (while running) the anchor plus the monotonic time since it.
    private TimeSpan StopwatchElapsed() =>
        _stopwatch.IsRunning ? _anchorElapsed + _time.GetElapsedTime(_anchorStamp) : _stopwatch.Banked;

    private void Anchor()
    {
        _anchorElapsed = _stopwatch.Elapsed(_time.GetUtcNow());
        _anchorStamp = _time.GetTimestamp();
    }

    public string StopwatchButtonText => StopwatchRunning ? "Pause" : _stopwatch.Banked > TimeSpan.Zero ? "Resume" : "Start";

    // The timer's length can only change while it is idle.
    public bool CanSetTimer => TimerStatus == TimerStatus.Idle;

    public ClockToolsViewModel(IClockToolsRepository? repository, TimeProvider time)
    {
        _repository = repository;
        _time = time;
        var state = repository?.Get() ?? new ClockToolsState(ClockSlotsConfig.Default, TimerState.Idle, StopwatchState.Idle);
        Slots = state.Slots.Normalized();
        _timer = state.Timer;
        _stopwatch = state.Stopwatch;
        Anchor();
        _announced = _timer.Status(_time.GetUtcNow()) == TimerStatus.Finished;
        Refresh(false);
    }

    public void Tick() => Refresh(true);

    // A running timer or stopwatch (or a finished timer), for the dots of a slot that is showing something else.
    public ToolIndicator IndicatorFor(ClockTool tool) => tool switch
    {
        ClockTool.Timer when TimerStatus == TimerStatus.Running => new(true, TimerText),
        ClockTool.Timer when TimerStatus == TimerStatus.Finished => new(true, "Done"),
        ClockTool.Stopwatch when StopwatchRunning => new(true, StopwatchText[..^2]),
        _ => default,
    };

    public void SetSlots(ClockSlotsConfig slots)
    {
        Slots = slots.Normalized();
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
        if (_stopwatch.IsRunning)
        {
            _stopwatch = _stopwatch.PauseAt(StopwatchElapsed());
        }
        else
        {
            _stopwatch = _stopwatch.Start(_time.GetUtcNow());
            Anchor();
        }
        Changed();
    }

    [RelayCommand]
    private void StopwatchLap()
    {
        _stopwatch = _stopwatch.LapAt(StopwatchElapsed());
        Changed();
    }

    [RelayCommand]
    private void StopwatchReset()
    {
        _stopwatch = _stopwatch.Reset();
        Changed();
    }

    private void Changed()
    {
        Refresh(false);
        Save();
    }

    private void Save() => _repository?.Save(new ClockToolsState(Slots, _timer, _stopwatch));

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

        StopwatchText = ClockToolsFormat.Stopwatch(StopwatchElapsed());
        StopwatchRunning = _stopwatch.IsRunning;
        SyncLaps();

        var buttons = (TimerButtonText, StopwatchButtonText, CanSetTimer);
        if (buttons == _buttons)
        {
            return;
        }
        _buttons = buttons;
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
