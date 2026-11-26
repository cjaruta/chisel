using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Chisel.Core.Settings;

namespace Chisel.App.Services;

// The show/hide shortcut through RegisterHotKey on the main window's handle. Windows posts WM_HOTKEY to that window, which
// arrives on the UI thread through Avalonia's window procedure hook. This is not a keyboard hook: nothing else is seen.
public sealed class Win32HotkeyService : IHotkeyService
{
    private const int HotkeyId = 0xC15E;
    private const uint WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private readonly Window _window;
    private readonly Win32Properties.CustomWndProcHookCallback _hook;
    private IntPtr _registeredOn;
    private Hotkey? _current;

    public Win32HotkeyService(Window window)
    {
        _window = window;
        _hook = WndProc;
        Win32Properties.AddWndProcHookCallback(window, _hook);
        // If the window's handle was ever recreated, the shortcut moves to the new one.
        window.Opened += (_, _) =>
        {
            if (_current is { } hotkey && Handle != _registeredOn)
            {
                Register(hotkey);
            }
        };
    }

    public event Action? Pressed;

    private IntPtr Handle => _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    public HotkeyRegistration Register(Hotkey hotkey)
    {
        Unregister();
        var handle = Handle;
        if (handle == IntPtr.Zero || HotkeyKeys.VirtualKey(hotkey.Key) is not { } virtualKey)
        {
            return HotkeyRegistration.Failed;
        }
        if (!RegisterHotKey(handle, HotkeyId, (uint)hotkey.Modifiers | ModNoRepeat, virtualKey))
        {
            return Marshal.GetLastWin32Error() == ErrorHotkeyAlreadyRegistered ? HotkeyRegistration.InUse : HotkeyRegistration.Failed;
        }
        _registeredOn = handle;
        _current = hotkey;
        return HotkeyRegistration.Registered;
    }

    public void Unregister()
    {
        if (_registeredOn != IntPtr.Zero)
        {
            UnregisterHotKey(_registeredOn, HotkeyId);
        }
        _registeredOn = IntPtr.Zero;
        _current = null;
    }

    public void Dispose()
    {
        Unregister();
        Win32Properties.RemoveWndProcHookCallback(_window, _hook);
    }

    private IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
