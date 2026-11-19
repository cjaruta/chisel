using Chisel.Core.Settings;

namespace Chisel.Infrastructure.Data;

public class UserSettingsEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeOnly WakeTime { get; set; }
    public TimeOnly BedTime { get; set; }
    public int ClockFormat { get; set; }

    // Id of the schedule color palette.
    public int TemperatureUnit { get; set; }
    public bool MediaVisualizer { get; set; } = true;
    public bool MediaVolume { get; set; } = true;
    public bool MediaScrollVolume { get; set; }
    public string ThemeMode { get; set; } = UserSettings.DefaultThemeMode;
    public string? AccentColor { get; set; }
    public string SchedulePalette { get; set; } = UserSettings.DefaultSchedulePalette;
}