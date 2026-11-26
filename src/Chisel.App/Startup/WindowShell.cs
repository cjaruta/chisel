using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Chisel.App.Startup;

// The real main window behind IShellWindow. A window that started hidden in the tray becomes the lifetime's main window the
// first time it is shown; restoring from minimized goes back to normal or maximized, whichever it was.
public sealed class WindowShell : IShellWindow
{
    private readonly Window _window;
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private WindowState _restoreState = WindowState.Normal;

    public WindowShell(Window window, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _window = window;
        _desktop = desktop;
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty && window.WindowState != WindowState.Minimized)
            {
                _restoreState = window.WindowState;
            }
        };
    }

    public bool IsVisible => _window.IsVisible;
    public bool IsActive => _window.IsActive;
    public bool IsMinimized => _window.WindowState == WindowState.Minimized;

    public void ShowAndActivate()
    {
        _desktop.MainWindow ??= _window;
        if (!_window.IsVisible)
        {
            _window.Show();
        }
        if (IsMinimized)
        {
            _window.WindowState = _restoreState;
        }
        _window.Activate();
    }

    public void Hide() => _window.Hide();

    public void Minimize() => _window.WindowState = WindowState.Minimized;
}
