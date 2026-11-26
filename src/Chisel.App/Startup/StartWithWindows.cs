using System;
using System.IO;
using System.Security;

namespace Chisel.App.Startup;

// The per-user Run key (HKCU\Software\Microsoft\Windows\CurrentVersion\Run). Chisel only ever touches its own value there.
public interface IStartupRegistry
{
    string? Read(string name);
    void Write(string name, string value);
    void Delete(string name);
}

public sealed record StartupState(bool IsOn, string? Error);

// Starting Chisel with Windows through its one Run value. The registry is the truth: nothing about it is saved elsewhere.
// Every call returns a readable message instead of throwing when Windows refuses (for example a policy blocks the Run key).
public sealed class StartWithWindows
{
    public const string ValueName = "Chisel";
    private const string Blocked = "Windows didn't let Chisel change its startup entry. It may be blocked by a policy on this PC.";

    private readonly IStartupRegistry _registry;
    private readonly string? _programPath;

    // programPath: the running Chisel.App.exe; null when it can't be found, which keeps the feature off.
    public StartWithWindows(IStartupRegistry registry, string? programPath)
    {
        _registry = registry;
        _programPath = programPath;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_programPath);

    // The Run value: the program path in quotes (it may contain spaces), then the switch that starts it to the tray.
    public string? Command => IsAvailable ? $"\"{_programPath}\" {StartupArguments.MinimizedSwitch}" : null;

    public StartupState Read()
    {
        try
        {
            return new StartupState(!string.IsNullOrEmpty(_registry.Read(ValueName)), null);
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            return new StartupState(false, Blocked);
        }
    }

    // Returns null when done, or why it couldn't be.
    public string? SetEnabled(bool on)
    {
        if (on && Command is null)
        {
            return "Chisel couldn't find its own program file, so it can't start with Windows.";
        }
        try
        {
            if (on)
            {
                _registry.Write(ValueName, Command!);
            }
            else
            {
                _registry.Delete(ValueName);
            }
            return null;
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            return Blocked;
        }
    }

    // At launch: when the entry exists but points somewhere else (Chisel was moved), point it here. Off stays off.
    public string? Revalidate()
    {
        try
        {
            var current = _registry.Read(ValueName);
            if (string.IsNullOrEmpty(current) || Command is null || string.Equals(current, Command, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            _registry.Write(ValueName, Command);
            return null;
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            return Blocked;
        }
    }

    private static bool IsRefusal(Exception ex) => ex is UnauthorizedAccessException or SecurityException or IOException;
}
