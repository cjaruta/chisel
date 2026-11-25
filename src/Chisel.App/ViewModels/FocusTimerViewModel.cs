using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Chisel.App.Services;
using Chisel.Core.Focus;

namespace Chisel.App.ViewModels;

// A task that can be the focus: read from the task list, never changed from here.
public sealed record FocusTaskOption(Guid Id, string Name, bool IsDone);

// The focus timer widget's state: the Pomodoro cycle (FocusTimer, which works from timestamps), its options, the task in focus, and
// what the widget shows. One timer drives it. While a widget is watching and the timer runs, it wakes exactly when the shown
// second changes; while nobody watches it wakes only at the end of the phase (to chime and move on); when stopped it wakes only at
// local midnight (today's count starts over) or to end the flash. The shown text is rebuilt only when it changes, so a wake-up that
// changes nothing allocates nothing. Every change of state or option is saved at once.
public sealed class FocusTimerViewModel : ViewModelBase, IDisposable
{
    // How long the widget stays highlighted after a phase ends (or until any button is pressed).
    public static readonly TimeSpan FlashLength = TimeSpan.FromSeconds(8);

    private const int DefaultVolume = 40;

    private readonly IFocusTimerRepository? _repository;
    private readonly TimeProvider _time;
    private readonly IChime _chime;
    private readonly Action<Action> _dispatch;
    private readonly Func<IReadOnlyList<FocusTaskOption>> _tasks;
    private readonly FocusTimer _timer;
    private readonly ITimer _wake;
    private readonly List<object> _watchers = [];
    private DateTimeOffset? _flashUntil;
    private DateTimeOffset _nextWake = DateTimeOffset.MaxValue;
    private int _unmutedVolume = DefaultVolume;
    private bool _disposed;

    // What is shown now, to tell whether anything changed.
    private long _shownSeconds = -1;
    private FocusPhase _shownPhase;
    private FocusRun _shownRun;
    private int _shownSessions = -1;
    private int _shownDone = -1;
    private TimeSpan _shownDuration;

    public FocusTimerViewModel(IFocusTimerRepository? repository, TimeProvider time, IChime? chime = null, Action<Action>? dispatch = null,
        Func<IReadOnlyList<FocusTaskOption>>? tasks = null)
    {
        _repository = repository;
        _time = time;
        _chime = chime ?? new NoChime();
        _dispatch = dispatch ?? (action => action());
        _tasks = tasks ?? (() => []);
        var saved = repository?.Get() ?? FocusTimerData.Default;
        _timer = new FocusTimer(time, saved.Settings, saved.State);
        if (saved.Settings.ChimeVolume > 0)
        {
            _unmutedVolume = saved.Settings.ChimeVolume;
        }
        _wake = time.CreateTimer(_ => _dispatch(Tick), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        RefreshTask();
        Tick();
    }

    // Shown ------------------------------------------------------------------------------------------------------------------

    public FocusPhase Phase => _timer.Phase;

    public FocusRun Run => _timer.Run;

    public bool IsRunning => _timer.Run == FocusRun.Running;

    public string PhaseText { get; private set; } = "";

    public string TimeText { get; private set; } = "";

    // The part of the phase still to go, 1 at the start and 0 at the end (the ring's length).
    public double Fraction { get; private set; } = 1;

    public string AutomationName { get; private set; } = "";

    public string PrimaryText => _timer.Run switch { FocusRun.Running => "Pause", FocusRun.Paused => "Resume", _ => "Start" };

    public int SessionsToday => _timer.SessionsToday;

    public string SessionsText { get; private set; } = "";

    // The session dots: one per focus session in the cycle, the finished ones filled.
    public int DotsTotal => _timer.Settings.SessionsBeforeLongBreak;

    public int DotsDone => Math.Min(_timer.FocusDoneInCycle, DotsTotal);

    // Whether the phase is an hour or longer (its time shows hours).
    public bool LongTime => _timer.Duration >= TimeSpan.FromHours(1);

    public bool IsFlashing => _flashUntil is not null;

    // How many times the shown values were rebuilt (for tests of the timer).
    public int Updates { get; private set; }

    // Controls ---------------------------------------------------------------------------------------------------------------

    // The one button that starts, pauses and resumes.
    public void Primary()
    {
        if (_timer.Run == FocusRun.Running)
        {
            Pause();
        }
        else
        {
            Start();
        }
    }

    public void Start() => Act(_timer.Start);

    public void Pause() => Act(_timer.Pause);

    public void Resume() => Act(_timer.Resume);

    public void Skip() => Act(_timer.Skip);

    public void Reset() => Act(_timer.Reset);

    private void Act(Action action)
    {
        action();
        _flashUntil = null;
        Persist();
        Changed(force: true);
        Schedule();
    }

    // Options ----------------------------------------------------------------------------------------------------------------

    public FocusSettings Settings => _timer.Settings;

    // What was wrong with the last option entered (it was not applied), or null.
    public string? SettingsError { get; private set; }

    public int FocusMinutes { get => Settings.FocusMinutes; set => Apply(Settings with { FocusMinutes = value }); }

    public int ShortBreakMinutes { get => Settings.ShortBreakMinutes; set => Apply(Settings with { ShortBreakMinutes = value }); }

    public int LongBreakMinutes { get => Settings.LongBreakMinutes; set => Apply(Settings with { LongBreakMinutes = value }); }

    public int SessionsBeforeLongBreak { get => Settings.SessionsBeforeLongBreak; set => Apply(Settings with { SessionsBeforeLongBreak = value }); }

    public bool AutoStartNext { get => Settings.AutoStartNext; set => Apply(Settings with { AutoStartNext = value }); }

    public int ChimeVolume
    {
        get => Settings.ChimeVolume;
        set
        {
            if (value > 0)
            {
                _unmutedVolume = value;
            }
            Apply(Settings with { ChimeVolume = value });
        }
    }

    public bool IsMuted
    {
        get => Settings.ChimeVolume == 0;
        set => ChimeVolume = value ? 0 : _unmutedVolume;
    }

    public bool IsDigital { get => Settings.Face == FocusFace.Digital; set => Apply(Settings with { Face = value ? FocusFace.Digital : FocusFace.Ring }); }

    public IReadOnlyList<FocusPreset> Presets => FocusPresets.All;

    // The chime at the chosen volume, to try it.
    public void PlayChime()
    {
        if (Settings.ChimeVolume > 0)
        {
            _chime.Play(Settings.ChimeVolume);
        }
    }

    public void ApplyPreset(FocusPreset preset) => Apply(preset.ApplyTo(Settings));

    private void Apply(FocusSettings settings)
    {
        SettingsError = FocusValidation.Error(settings);
        if (SettingsError is null && settings != Settings)
        {
            _timer.Settings = settings;
            Persist();
            _timer.Update();
        }
        foreach (var name in new[]
        {
            nameof(SettingsError), nameof(FocusMinutes), nameof(ShortBreakMinutes), nameof(LongBreakMinutes), nameof(SessionsBeforeLongBreak),
            nameof(AutoStartNext), nameof(ChimeVolume), nameof(IsMuted), nameof(IsDigital), nameof(Settings),
        })
        {
            OnPropertyChanged(name);
        }
        Changed(force: true);
        Schedule();
    }

    // The task in focus ------------------------------------------------------------------------------------------------------

    public Guid? TaskId => Settings.TaskId;

    public string? TaskName { get; private set; }

    // The tasks that can be chosen: those not done, in the task list's order.
    public IReadOnlyList<FocusTaskOption> TaskOptions() => _tasks().Where(t => !t.IsDone).ToList();

    public void ChooseTask(Guid? id)
    {
        _timer.Settings = Settings with { TaskId = id };
        Persist();
        RefreshTask();
    }

    // Looks the task up again (its name may have changed); a task that is gone is let go.
    public void RefreshTask()
    {
        string? name = null;
        if (Settings.TaskId is { } id)
        {
            name = _tasks().FirstOrDefault(t => t.Id == id)?.Name;
            if (name is null)
            {
                _timer.Settings = Settings with { TaskId = null };
                Persist();
            }
        }
        TaskName = name;
        OnPropertyChanged(nameof(TaskName));
        OnPropertyChanged(nameof(TaskId));
    }

    // Updating ---------------------------------------------------------------------------------------------------------------

    public bool IsWatched => _watchers.Count > 0;

    // A widget showing the timer; while one is, the time is kept current to the second.
    public void Attach(object watcher)
    {
        if (!_watchers.Contains(watcher))
        {
            _watchers.Add(watcher);
            RefreshTask();
            Tick();
        }
    }

    public void Detach(object watcher)
    {
        if (_watchers.Remove(watcher))
        {
            Schedule();
        }
    }

    // Ends the phase if its time is up, ends the flash when due, updates what is shown, and sets the next wake-up.
    public void Tick()
    {
        if (_disposed)
        {
            return;
        }
        if (_timer.Update() is { } end)
        {
            Ended(end);
        }
        if (_flashUntil is { } until && _time.GetUtcNow() >= until)
        {
            _flashUntil = null;
            OnPropertyChanged(nameof(IsFlashing));
        }
        Changed(force: false);
        Schedule();
    }

    private void Ended(FocusPhaseEnd end)
    {
        Persist();
        if (end.Late < FocusTimer.AutoStartGrace && Settings.ChimeVolume > 0)
        {
            _chime.Play(Settings.ChimeVolume);
        }
        _flashUntil = _time.GetUtcNow() + FlashLength;
        OnPropertyChanged(nameof(IsFlashing));
    }

    // Rebuilds the shown values if any of them changed (or when forced).
    private void Changed(bool force)
    {
        var remaining = _timer.Remaining;
        var seconds = (remaining.Ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond;
        var duration = _timer.Duration;
        if (!force && seconds == _shownSeconds && _timer.Phase == _shownPhase && _timer.Run == _shownRun
            && _timer.SessionsToday == _shownSessions && _timer.FocusDoneInCycle == _shownDone && duration == _shownDuration)
        {
            return;
        }
        _shownSeconds = seconds;
        _shownPhase = _timer.Phase;
        _shownRun = _timer.Run;
        _shownSessions = _timer.SessionsToday;
        _shownDone = _timer.FocusDoneInCycle;
        _shownDuration = duration;

        PhaseText = PhaseName(_timer.Phase);
        TimeText = Clock(seconds);
        Fraction = duration > TimeSpan.Zero ? Math.Clamp(remaining / duration, 0, 1) : 0;
        AutomationName = $"{PhaseText}, {Spoken(seconds)} remaining" + (_timer.Run == FocusRun.Paused ? ", paused" : "");
        SessionsText = _shownSessions == 1 ? "1 session today" : $"{_shownSessions} sessions today";
        Updates++;
        foreach (var name in new[]
        {
            nameof(Phase), nameof(Run), nameof(IsRunning), nameof(PhaseText), nameof(TimeText), nameof(Fraction), nameof(AutomationName),
            nameof(PrimaryText), nameof(SessionsToday), nameof(SessionsText), nameof(DotsTotal), nameof(DotsDone), nameof(LongTime), nameof(IsFlashing), nameof(Updates),
        })
        {
            OnPropertyChanged(name);
        }
    }

    // The next wake-up: the next shown second while watched and running, the phase end while running unwatched, otherwise the
    // next local midnight (today's count); and the end of the flash if that is sooner. The timer is only reset when that moves.
    private void Schedule()
    {
        if (_disposed)
        {
            return;
        }
        var now = _time.GetUtcNow();
        TimeSpan due;
        if (_timer.Run == FocusRun.Running)
        {
            var remaining = _timer.Remaining;
            due = IsWatched && remaining > TimeSpan.Zero
                ? remaining - TimeSpan.FromSeconds(_shownSeconds - 1)
                : remaining;
        }
        else
        {
            var local = TimeZoneInfo.ConvertTime(now, _time.LocalTimeZone);
            var midnight = new DateTimeOffset(local.Date.AddDays(1), _time.LocalTimeZone.GetUtcOffset(local.Date.AddDays(1)));
            due = midnight - now;
        }
        if (_flashUntil is { } until && until - now < due)
        {
            due = until - now;
        }
        if (due < TimeSpan.FromMilliseconds(1))
        {
            due = TimeSpan.FromMilliseconds(1);
        }
        var at = now + due;
        if (at != _nextWake)
        {
            _nextWake = at;
            _wake.Change(due, Timeout.InfiniteTimeSpan);
        }
    }

    private void Persist() => _repository?.Save(new FocusTimerData(_timer.Settings, _timer.State));

    public void Dispose()
    {
        _disposed = true;
        _wake.Dispose();
    }

    // Text -------------------------------------------------------------------------------------------------------------------

    public static string PhaseName(FocusPhase phase) => phase switch
    {
        FocusPhase.ShortBreak => "Short Break",
        FocusPhase.LongBreak => "Long Break",
        _ => "Focus",
    };

    // "25:00", or "1:05:00" from an hour.
    private static string Clock(long seconds) => seconds >= 3600
        ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}"
        : $"{seconds / 60}:{seconds % 60:00}";

    // "18 minutes 20 seconds", "1 hour 5 minutes", "0 seconds".
    private static string Spoken(long seconds)
    {
        var parts = new List<string>(3);
        void Add(long n, string unit)
        {
            if (n > 0)
            {
                parts.Add(n == 1 ? $"1 {unit}" : $"{n} {unit}s");
            }
        }
        Add(seconds / 3600, "hour");
        Add(seconds / 60 % 60, "minute");
        Add(seconds % 60, "second");
        return parts.Count == 0 ? "0 seconds" : string.Join(" ", parts);
    }
}
