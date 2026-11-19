namespace Chisel.Core.Settings;

public class UserSettings
{
    // Id of the schedule color palette. The app knows which ids exist and falls back for unknown ones.
    public const string DefaultSchedulePalette = "minimal";

    public UserSettings(string name, TimeOnly wakeTime, TimeOnly bedTime, ClockFormat clockFormat = ClockFormat.TwentyFourHour,
        string? schedulePalette = null, TemperatureUnit temperatureUnit = TemperatureUnit.Celsius, bool mediaVisualizer = true, bool mediaVolume = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        if (wakeTime == bedTime)
        {
            throw new ArgumentException("Wake time and bed time cannot be the same.", nameof(wakeTime));
        }

        Name = name.Trim();
        WakeTime = wakeTime;
        BedTime = bedTime;
        ClockFormat = clockFormat;
        TemperatureUnit = temperatureUnit;
        MediaVisualizer = mediaVisualizer;
        MediaVolume = mediaVolume;
        SchedulePalette = string.IsNullOrWhiteSpace(schedulePalette) ? DefaultSchedulePalette : schedulePalette.Trim();
    }

    public string Name { get; }
    public TimeOnly WakeTime { get; }
    public TimeOnly BedTime { get; }
    public ClockFormat ClockFormat { get; }
    public string SchedulePalette { get; }
    public TemperatureUnit TemperatureUnit { get; }
    // Whether the media widget shows its audio bars.
    public bool MediaVisualizer { get; }
    // Whether the media widget shows its volume row.
    public bool MediaVolume { get; }

    public UserSettings WithClockFormat(ClockFormat clockFormat) => new(Name, WakeTime, BedTime, clockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume);

    public UserSettings WithSchedulePalette(string schedulePalette) => new(Name, WakeTime, BedTime, ClockFormat, schedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume);

    public UserSettings WithTemperatureUnit(TemperatureUnit unit) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, unit, MediaVisualizer, MediaVolume);

    public UserSettings WithMediaVisualizer(bool on) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, on, MediaVolume);

    public UserSettings WithMediaVolume(bool on) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, on);
}
