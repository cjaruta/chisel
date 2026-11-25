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

// The Run key as a dictionary. Blocked makes every read and write fail the way a policy does.
public sealed class FakeStartupRegistry : IStartupRegistry
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Blocked { get; set; }
    public int Writes { get; private set; }

    public string? Read(string name)
    {
        Check();
        return Values.GetValueOrDefault(name);
    }

    public void Write(string name, string value)
    {
        Check();
        Writes++;
        Values[name] = value;
    }

    public void Delete(string name)
    {
        Check();
        Values.Remove(name);
    }

    private void Check()
    {
        if (Blocked)
        {
            throw new UnauthorizedAccessException("blocked");
        }
    }
}
