using System;
using Chisel.Core.Settings;

namespace Chisel.App.Services;

public enum HotkeyRegistration { Registered, InUse, Failed }

// One system-wide shortcut, registered with Windows (RegisterHotKey). No keyboard hooks: Windows only tells us when
// that exact shortcut is pressed. Pressed is raised on the UI thread.
public interface IHotkeyService : IDisposable
{
    // Replaces whatever shortcut was registered before.
    HotkeyRegistration Register(Hotkey hotkey);
    void Unregister();
    event Action? Pressed;
}
