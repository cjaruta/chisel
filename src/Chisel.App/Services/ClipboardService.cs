using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Chisel.Core.Clipboard;

namespace Chisel.App.Services;

// The clipboard as the clipboard history widget sees it. Nothing listens until Listen is called, and listening ends when the last
// returned handle is disposed.
public interface IClipboardService
{
    bool IsListening { get; }

    // Calls changed (on any thread) each time the clipboard changes, until the handle is disposed.
    IDisposable Listen(Action changed);

    // Reads the clipboard off the UI thread. Text is read only when no opt-out format is set and no password manager owns it, and
    // at most maxChars + 1 characters are read (enough to tell the text is over the limit).
    Task<ClipboardSnapshot> ReadAsync(int maxChars);

    Task SetTextAsync(string text);
}

// The system's clipboard change notification, started and stopped by SharedClipboardListener.
public interface IClipboardHook
{
    void Start(Action changed);
    void Stop();
}

// Diagnostics for the clipboard history. Callers only ever write counts, lengths and reasons: never clipboard text.
public interface IClipboardHistoryLog
{
    void Write(string message);
}

public sealed class DebugClipboardLog : IClipboardHistoryLog
{
    public void Write(string message) => System.Diagnostics.Debug.WriteLine("[Clipboard] " + message);
}

// One system listener shared by every widget that listens: the hook starts with the first listener and stops with the last.
// Raising a change walks an array that is replaced (not changed) when listeners come and go, so a change allocates nothing.
public sealed class SharedClipboardListener
{
    private readonly IClipboardHook _hook;
    private readonly object _gate = new();
    private Action[] _listeners = [];

    public SharedClipboardListener(IClipboardHook hook)
    {
        _hook = hook;
    }

    public bool IsListening => Volatile.Read(ref _listeners).Length > 0;

    public IDisposable Listen(Action changed)
    {
        lock (_gate)
        {
            var first = _listeners.Length == 0;
            Volatile.Write(ref _listeners, [.. _listeners, changed]);
            if (first)
            {
                _hook.Start(Raise);
            }
        }
        return new Subscription(this, changed);
    }

    private void Leave(Action changed)
    {
        lock (_gate)
        {
            var index = Array.IndexOf(_listeners, changed);
            if (index < 0)
            {
                return;
            }
            var rest = new Action[_listeners.Length - 1];
            Array.Copy(_listeners, 0, rest, 0, index);
            Array.Copy(_listeners, index + 1, rest, index, rest.Length - index);
            Volatile.Write(ref _listeners, rest);
            if (rest.Length == 0)
            {
                _hook.Stop();
            }
        }
    }

    private void Raise()
    {
        foreach (var listener in Volatile.Read(ref _listeners))
        {
            listener();
        }
    }

    private sealed class Subscription(SharedClipboardListener owner, Action changed) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Leave(changed);
            }
        }
    }
}

// The Windows clipboard: a format listener on its own message-only window, reads done on a worker thread with short retries while
// another app has the clipboard open, and writes through the main window's clipboard. No network, no polling.
public sealed class WindowsClipboardService : IClipboardService
{
    private readonly SharedClipboardListener _listener = new(new Win32ClipboardHook());
    private readonly Func<IClipboard?> _clipboard;
    private readonly IClipboardHistoryLog? _log;

    public WindowsClipboardService(Func<IClipboard?> clipboard, IClipboardHistoryLog? log = null)
    {
        _clipboard = clipboard;
        _log = log;
    }

    public bool IsListening => _listener.IsListening;

    public IDisposable Listen(Action changed) => _listener.Listen(changed);

    public Task<ClipboardSnapshot> ReadAsync(int maxChars) => Task.Run(() => Win32Clipboard.Read(maxChars, _log));

    public Task SetTextAsync(string text) => _clipboard() is { } clipboard ? clipboard.SetTextAsync(text) : Task.CompletedTask;
}

// For tests and anywhere without a system clipboard: never changes, holds nothing.
public sealed class NoClipboard : IClipboardService
{
    private readonly SharedClipboardListener _listener = new(new NoHook());

    public bool IsListening => _listener.IsListening;

    public IDisposable Listen(Action changed) => _listener.Listen(changed);

    public Task<ClipboardSnapshot> ReadAsync(int maxChars) => Task.FromResult(new ClipboardSnapshot(null));

    public Task SetTextAsync(string text) => Task.CompletedTask;

    private sealed class NoHook : IClipboardHook
    {
        public void Start(Action changed) { }
        public void Stop() { }
    }
}
