namespace Chisel.Core.Settings;

// App-level choices: what closing the window does, whether a start with Windows opens straight to the tray, and the
// show/hide shortcut ("" means the shortcut is off). Whether Chisel starts with Windows is not kept here: the Run
// registry value itself is the truth.
public sealed record SystemSettings(bool CloseToTray, bool StartMinimized, string Hotkey)
{
    public const string DefaultHotkey = "Ctrl+Alt+C";

    public static SystemSettings Default { get; } = new(false, false, DefaultHotkey);
}

public interface ISystemSettingsRepository
{
    SystemSettings Get();
    void Save(SystemSettings settings);
}
