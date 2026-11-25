using Chisel.App.Services;
using Chisel.Core.Clipboard;

namespace Chisel.App.Tests.Support;

// Stands in for the system's clipboard listener: counts starts and stops, and fires changes only while started.
public sealed class FakeClipboardHook : IClipboardHook
{
    private Action? _changed;

    public int Starts { get; private set; }
    public int Stops { get; private set; }
    public bool IsStarted => _changed is not null;

    public void Start(Action changed)
    {
        Starts++;
        _changed = changed;
    }

    public void Stop()
    {
        Stops++;
        _changed = null;
    }

    public void Fire() => _changed?.Invoke();
}

// A clipboard held in memory. Copy puts text on it as another app would; SetTextAsync is the widget's own copy, which the
// system reports back as a change like any other.
public sealed class FakeClipboard : IClipboardService
{
    public FakeClipboardHook Hook { get; } = new();
    private readonly SharedClipboardListener _listener;

    public FakeClipboard() => _listener = new SharedClipboardListener(Hook);

    public ClipboardSnapshot Current { get; private set; } = new(null);
    public int MaxCharsAsked { get; private set; }

    public bool IsListening => _listener.IsListening;

    public IDisposable Listen(Action changed) => _listener.Listen(changed);

    public Task<ClipboardSnapshot> ReadAsync(int maxChars)
    {
        MaxCharsAsked = maxChars;
        return Task.FromResult(Current);
    }

    public Task SetTextAsync(string text)
    {
        Copy(text);
        return Task.CompletedTask;
    }

    public void Copy(string? text, ClipboardFlags flags = ClipboardFlags.None, string? owner = null)
    {
        Current = new ClipboardSnapshot(text, flags, owner);
        Hook.Fire();
    }
}

public sealed class FakeClipboardStore : IClipboardHistoryStore
{
    public List<ClipboardEntry>? Saved { get; private set; }
    public int Saves { get; private set; }
    public int Deletes { get; private set; }

    public string Location => @"C:\Data\Chisel\clipboard-history.json";

    public IReadOnlyList<ClipboardEntry> Load() => Saved ?? [];

    public void Save(IReadOnlyList<ClipboardEntry> entries)
    {
        Saves++;
        Saved = entries.ToList();
    }

    public void Delete()
    {
        Deletes++;
        Saved = null;
    }
}

public sealed class FakeClipboardSettings : IClipboardHistorySettingsRepository
{
    public ClipboardHistorySettings Saved { get; set; } = ClipboardHistorySettings.Default;

    public ClipboardHistorySettings Get() => Saved;

    public void Save(ClipboardHistorySettings settings) => Saved = settings;
}

public sealed class CapturingClipboardLog : IClipboardHistoryLog
{
    public List<string> Lines { get; } = new();

    public void Write(string message) => Lines.Add(message);
}
