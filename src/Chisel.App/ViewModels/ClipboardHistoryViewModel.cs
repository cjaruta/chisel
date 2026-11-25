using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Chisel.App.Services;
using Chisel.Core.Clipboard;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// The clipboard history widget. Privacy first:
//  - Nothing listens until Start, and recording always begins off when the app opens. Stop, Pause, taking the widget off the
//    dashboard and Dispose each release the system listener at once.
//  - Only text is recorded. Every change goes through ClipboardFilter (opt-out formats, password managers, the length limit and the
//    secret heuristics) before it is kept.
//  - History is in memory unless KeepHistory is on; turning it off deletes the saved file.
//  - The log gets counts, lengths and reasons only: never clipboard text.
public partial class ClipboardHistoryViewModel : ViewModelBase, IDisposable
{
    public static readonly TimeSpan CopiedFor = TimeSpan.FromSeconds(1.5);

    private readonly IClipboardService _clipboard;
    private readonly IClipboardHistorySettingsRepository? _settingsRepository;
    private readonly IClipboardHistoryStore? _store;
    private readonly TimeProvider _time;
    private readonly Action<Action> _dispatch;
    private readonly IClipboardHistoryLog? _log;
    private readonly ClipboardHistory _history;
    private readonly Action _onChanged;
    private ClipboardHistorySettings _settings;
    private IDisposable? _listener;
    private int _generation; // bumped on every start, pause and stop, so a read that was under way is dropped
    private string? _selfCopy; // text the widget just put on the clipboard, so its echo isn't recorded
    private ITimer? _copiedTimer;
    private bool _isOnDashboard;

    [ObservableProperty]
    public partial bool IsRecording { get; private set; }

    [ObservableProperty]
    public partial bool IsPaused { get; private set; }

    // The item just copied back, while the "Copied" note shows.
    [ObservableProperty]
    public partial Guid? CopiedId { get; private set; }

    public ClipboardHistoryViewModel(IClipboardService? clipboard, IClipboardHistorySettingsRepository? settings,
        IClipboardHistoryStore? store, TimeProvider? time = null, Action<Action>? dispatch = null, IClipboardHistoryLog? log = null)
    {
        _clipboard = clipboard ?? new NoClipboard();
        _settingsRepository = settings;
        _store = store;
        _time = time ?? TimeProvider.System;
        _dispatch = dispatch ?? (action => Dispatcher.UIThread.Post(action));
        _log = log;
        _onChanged = OnClipboardChanged;
        _settings = (settings?.Get() ?? ClipboardHistorySettings.Default).Valid();
        _history = new ClipboardHistory(_settings.MaxItems);
        if (_settings.KeepHistory)
        {
            _history.Load(_store?.Load() ?? []);
        }
        else
        {
            _store?.Delete(); // nothing may be left on disk from a time the setting was on
        }
    }

    // Pinned first, then newest first.
    public IReadOnlyList<ClipboardEntry> Items => _history.Items;

    // The items matching the search (all of them when it is blank).
    public IReadOnlyList<ClipboardEntry> Visible => string.IsNullOrWhiteSpace(Query) ? _history.Items : _history.Search(Query).ToList();

    // The most recent copy, pinned or not.
    public ClipboardEntry? Latest => _history.Items.Count == 0 ? null : _history.Items.MaxBy(i => i.CopiedAt);

    public DateTimeOffset Now => _time.GetUtcNow();

    public bool IsListening => _listener is not null;

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    partial void OnQueryChanged(string value) => OnPropertyChanged(nameof(Visible));

    // Set by the main view model from the layout. Taking the widget off stops recording.
    public bool IsOnDashboard
    {
        get => _isOnDashboard;
        set
        {
            _isOnDashboard = value;
            if (!value)
            {
                Stop();
            }
        }
    }

    public string? StorageLocation => _store?.Location;

    public bool KeepHistory
    {
        get => _settings.KeepHistory;
        set
        {
            if (value == _settings.KeepHistory)
            {
                return;
            }
            SaveSettings(_settings with { KeepHistory = value });
            if (value)
            {
                _store?.Save(_history.Items);
            }
            else
            {
                _store?.Delete();
            }
            _log?.Write(value ? "keeping history between sessions" : "stopped keeping history; saved file deleted");
            OnPropertyChanged();
        }
    }

    public int MaxItems
    {
        get => _settings.MaxItems;
        set
        {
            SaveSettings(_settings with { MaxItems = value });
            _history.MaxItems = _settings.MaxItems;
            OnPropertyChanged();
            ItemsChanged();
        }
    }

    public int MaxLength
    {
        get => _settings.MaxLength;
        set
        {
            SaveSettings(_settings with { MaxLength = value });
            OnPropertyChanged();
        }
    }

    public void Start()
    {
        if (IsRecording)
        {
            return;
        }
        IsRecording = true;
        IsPaused = false;
        Listen();
        _log?.Write("recording started");
    }

    public void Stop()
    {
        if (!IsRecording)
        {
            return;
        }
        IsRecording = false;
        IsPaused = false;
        Release();
        _log?.Write("recording stopped");
    }

    public void TogglePause()
    {
        if (!IsRecording)
        {
            return;
        }
        IsPaused = !IsPaused;
        if (IsPaused)
        {
            Release();
        }
        else
        {
            Listen();
        }
        _log?.Write(IsPaused ? "paused" : "resumed");
    }

    public void ClearAll()
    {
        _history.Clear();
        if (_settings.KeepHistory)
        {
            _store?.Delete();
        }
        _log?.Write("history cleared");
        ItemsChanged(persist: false);
    }

    public void Delete(Guid id)
    {
        if (_history.Remove(id))
        {
            _log?.Write("item deleted");
            ItemsChanged();
        }
    }

    public void TogglePin(Guid id)
    {
        if (_history.Items.FirstOrDefault(i => i.Id == id) is { } entry)
        {
            _history.SetPinned(id, !entry.Pinned);
            ItemsChanged();
        }
    }

    // Puts an item back on the clipboard. The change this causes is not recorded again.
    public async Task CopyAsync(Guid id)
    {
        if (_history.Items.FirstOrDefault(i => i.Id == id) is not { } entry)
        {
            return;
        }
        _selfCopy = IsListening ? entry.Text : null;
        await _clipboard.SetTextAsync(entry.Text);
        _log?.Write($"item copied back ({entry.Text.Length} chars)");
        CopiedId = id;
        _copiedTimer?.Dispose();
        _copiedTimer = _time.CreateTimer(_ => _dispatch(() =>
        {
            CopiedId = null;
            _selfCopy = null;
        }), null, CopiedFor, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        Stop();
        Release();
        _copiedTimer?.Dispose();
    }

    private void Listen()
    {
        _generation++;
        _listener ??= _clipboard.Listen(_onChanged);
        OnPropertyChanged(nameof(IsListening));
    }

    private void Release()
    {
        _generation++;
        _listener?.Dispose();
        _listener = null;
        _selfCopy = null;
        OnPropertyChanged(nameof(IsListening));
    }

    // Called on the listener's thread: hand over to the UI thread, which reads the clipboard (off-thread) and records it.
    private void OnClipboardChanged()
    {
        _dispatch(() => _ = CaptureAsync(_generation));
    }

    private bool Active(int generation) => IsRecording && !IsPaused && generation == _generation;

    private async Task CaptureAsync(int generation)
    {
        if (!Active(generation))
        {
            return;
        }
        ClipboardSnapshot snapshot;
        try
        {
            snapshot = await _clipboard.ReadAsync(_settings.MaxLength);
        }
        catch (Exception e)
        {
            _log?.Write($"read failed ({e.GetType().Name})"); // the type only: a message could quote what was read
            return;
        }
        if (!Active(generation))
        {
            return;
        }
        if (_selfCopy is { } self && string.Equals(snapshot.Text, self, StringComparison.Ordinal))
        {
            _selfCopy = null;
            _log?.Write("own copy ignored");
            return;
        }
        var skip = ClipboardFilter.Check(snapshot, _settings);
        if (skip != ClipboardSkip.None)
        {
            _log?.Write($"copy skipped ({skip})");
            return;
        }
        _history.Add(snapshot.Text!, _time.GetUtcNow());
        _log?.Write($"copy recorded ({snapshot.Text!.Length} chars)");
        ItemsChanged();
    }

    private void ItemsChanged(bool persist = true)
    {
        if (persist && _settings.KeepHistory)
        {
            _store?.Save(_history.Items);
        }
        OnPropertyChanged(nameof(Items));
        OnPropertyChanged(nameof(Visible));
        OnPropertyChanged(nameof(Latest));
    }

    private void SaveSettings(ClipboardHistorySettings settings)
    {
        _settings = settings.Valid();
        _settingsRepository?.Save(_settings);
    }
}
