using Chisel.App.Startup;
using Chisel.Core.Settings;

namespace Chisel.App.Tests.Support;

public sealed class MemorySystemSettings(SystemSettings? start = null) : ISystemSettingsRepository
{
    public SystemSettings Saved { get; private set; } = start ?? SystemSettings.Default;
    public int Saves { get; private set; }
    public SystemSettings Get() => Saved;
    public void Save(SystemSettings settings) { Saved = settings; Saves++; }
}

// A main window that only records what it was asked to do.
public sealed class FakeShellWindow : IShellWindow
{
    public bool IsVisible { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool IsMinimized { get; set; }
    public int Shows { get; private set; }

    public void ShowAndActivate() { Shows++; IsVisible = true; IsActive = true; IsMinimized = false; }
    public void Hide() { IsVisible = false; IsActive = false; }
    public void Minimize() { IsMinimized = true; IsActive = false; }
}
