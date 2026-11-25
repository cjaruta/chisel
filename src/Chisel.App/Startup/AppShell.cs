using System;
using Chisel.Core.Settings;

namespace Chisel.App.Startup;

// The main window as the shell needs it.
public interface IShellWindow
{
    bool IsVisible { get; }
    bool IsActive { get; }
    bool IsMinimized { get; }
    void ShowAndActivate();
    void Hide();
    void Minimize();
}

// Decides what closing, the tray icon and the show/hide shortcut do with the main window. The window is only ever hidden
// while a tray icon exists, so the app is never left running with neither a window nor a tray icon.
public sealed class AppShell
{
    // Clicking the tray icon takes focus from the window first, so a window that lost focus this recently counts as active.
    private static readonly TimeSpan FocusGrace = TimeSpan.FromMilliseconds(500);
    // The second click of a double click is ignored, so a double click shows the window rather than showing and hiding it.
    private static readonly TimeSpan DoubleClick = TimeSpan.FromMilliseconds(500);

    private readonly IShellWindow _window;
    private readonly Func<SystemSettings> _settings;
    private readonly bool _hasTray;
    private readonly Action _shutdown;
    private readonly TimeProvider _time;
    private DateTimeOffset? _deactivatedAt;
    private DateTimeOffset? _trayClickedAt;

    public AppShell(IShellWindow window, Func<SystemSettings> settings, bool hasTray, Action shutdown, TimeProvider? time = null)
    {
        _window = window;
        _settings = settings;
        _hasTray = hasTray;
        _shutdown = shutdown;
        _time = time ?? TimeProvider.System;
    }

    public bool IsQuitting { get; private set; }

    private bool HidesToTray => _hasTray && _settings().CloseToTray;

    public bool ShouldStartHidden(bool launchedMinimized) => _hasTray && launchedMinimized && _settings().StartMinimized;

    // From the window's Closing event: true cancels the close because the window went to the tray instead.
    // Only a close by the user is turned into a hide; quitting, Windows shutting down and closes by the app itself go through.
    public bool HandleClosing(bool userClose)
    {
        if (IsQuitting || !userClose || !HidesToTray)
        {
            return false;
        }
        _window.Hide();
        return true;
    }

    public void NoteDeactivated() => _deactivatedAt = _time.GetUtcNow();

    // The show/hide shortcut: brings the window forward, or puts it away when it is already in front.
    public void Toggle()
    {
        if (_window.IsVisible && !_window.IsMinimized && _window.IsActive)
        {
            PutAway();
        }
        else
        {
            _window.ShowAndActivate();
        }
    }

    public void TrayClicked()
    {
        var now = _time.GetUtcNow();
        if (_trayClickedAt is { } last && now - last < DoubleClick)
        {
            return;
        }
        _trayClickedAt = now;
        var justActive = _deactivatedAt is { } lost && now - lost < FocusGrace;
        if (_window.IsVisible && !_window.IsMinimized && (_window.IsActive || justActive))
        {
            PutAway();
        }
        else
        {
            _window.ShowAndActivate();
        }
    }

    // The tray menu takes focus while it is open, so Show/Hide goes by whether the window is on screen.
    public void MenuShowHide()
    {
        if (_window.IsVisible && !_window.IsMinimized)
        {
            PutAway();
        }
        else
        {
            _window.ShowAndActivate();
        }
    }

    public void Show() => _window.ShowAndActivate();

    public void Quit()
    {
        IsQuitting = true;
        _shutdown();
    }

    private void PutAway()
    {
        if (HidesToTray)
        {
            _window.Hide();
        }
        else
        {
            _window.Minimize();
        }
    }
}
