namespace Chisel.Core.Settings;

public class UserSettings
{
    // Id of the schedule color palette. The app knows which ids exist and falls back for unknown ones.
    public const string DefaultSchedulePalette = "minimal";
    // Id of the theme mode (see Theme.ThemeModes); the app falls back to Default for unknown ids.
    public const string DefaultThemeMode = "default";

    public UserSettings(string name, TimeOnly wakeTime, TimeOnly bedTime, ClockFormat clockFormat = ClockFormat.TwentyFourHour,
        string? schedulePalette = null, TemperatureUnit temperatureUnit = TemperatureUnit.Celsius, bool mediaVisualizer = true, bool mediaVolume = true, bool mediaScrollVolume = false,
        string? themeMode = null, string? accentColor = null, bool mediaAlbumGradient = true, bool mediaAnimateGradient = true,
        bool pictureKeepOriginals = true, Pictures.MultiDropMode pictureMultiDrop = Pictures.MultiDropMode.OneSlideshow)
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
        MediaScrollVolume = mediaScrollVolume;
        MediaAlbumGradient = mediaAlbumGradient;
        MediaAnimateGradient = mediaAnimateGradient;
        PictureKeepOriginals = pictureKeepOriginals;
        PictureMultiDrop = pictureMultiDrop;
        ThemeMode = string.IsNullOrWhiteSpace(themeMode) ? DefaultThemeMode : themeMode.Trim();
        // Only a valid hex color is kept; null means the default accent.
        AccentColor = Theme.Rgb.TryParse(accentColor, out var accent) ? accent.ToHex() : null;
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
    public bool MediaScrollVolume { get; }
    // Whether the media widget paints a gradient from the cover art, and whether that gradient drifts slowly while playing.
    public bool MediaAlbumGradient { get; }
    public bool MediaAnimateGradient { get; }
    // Picture frames: whether dropped files are stored as they came (off: a display-size copy), and what dropping several makes.
    public bool PictureKeepOriginals { get; }
    public Pictures.MultiDropMode PictureMultiDrop { get; }
    public string ThemeMode { get; }
    // The chosen accent as #RRGGBB, or null for the default orange.
    public string? AccentColor { get; }

    public UserSettings WithClockFormat(ClockFormat clockFormat) => new(Name, WakeTime, BedTime, clockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume, MediaScrollVolume, ThemeMode, AccentColor, MediaAlbumGradient, MediaAnimateGradient, PictureKeepOriginals, PictureMultiDrop);

    public UserSettings WithSchedulePalette(string schedulePalette) => new(Name, WakeTime, BedTime, ClockFormat, schedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume, MediaScrollVolume, ThemeMode, AccentColor, MediaAlbumGradient, MediaAnimateGradient, PictureKeepOriginals, PictureMultiDrop);

    public UserSettings WithTemperatureUnit(TemperatureUnit unit) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, unit, MediaVisualizer, MediaVolume, MediaScrollVolume, ThemeMode, AccentColor, MediaAlbumGradient, MediaAnimateGradient, PictureKeepOriginals, PictureMultiDrop);

    public UserSettings WithMediaVisualizer(bool on) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, on, MediaVolume, MediaScrollVolume, ThemeMode, AccentColor, MediaAlbumGradient, MediaAnimateGradient, PictureKeepOriginals, PictureMultiDrop);

    public UserSettings WithMediaVolume(bool on) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, on, MediaScrollVolume, ThemeMode, AccentColor, MediaAlbumGradient, MediaAnimateGradient, PictureKeepOriginals, PictureMultiDrop);

    public UserSettings WithMediaScrollVolume(bool on) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume, on, ThemeMode, AccentColor, MediaAlbumGradient, MediaAnimateGradient, PictureKeepOriginals, PictureMultiDrop);

    public UserSettings WithMediaAlbumGradient(bool on) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume, MediaScrollVolume, ThemeMode, AccentColor, on, MediaAnimateGradient, PictureKeepOriginals, PictureMultiDrop);

    public UserSettings WithMediaAnimateGradient(bool on) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume, MediaScrollVolume, ThemeMode, AccentColor, MediaAlbumGradient, on, PictureKeepOriginals, PictureMultiDrop);

    public UserSettings WithThemeMode(string themeMode) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume, MediaScrollVolume, themeMode, AccentColor, MediaAlbumGradient, MediaAnimateGradient, PictureKeepOriginals, PictureMultiDrop);

    public UserSettings WithAccentColor(string? accentColor) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume, MediaScrollVolume, ThemeMode, accentColor, MediaAlbumGradient, MediaAnimateGradient, PictureKeepOriginals, PictureMultiDrop);

    public UserSettings WithPictureKeepOriginals(bool on) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume, MediaScrollVolume, ThemeMode, AccentColor, MediaAlbumGradient, MediaAnimateGradient, on, PictureMultiDrop);

    public UserSettings WithPictureMultiDrop(Pictures.MultiDropMode mode) => new(Name, WakeTime, BedTime, ClockFormat, SchedulePalette, TemperatureUnit, MediaVisualizer, MediaVolume, MediaScrollVolume, ThemeMode, AccentColor, MediaAlbumGradient, MediaAnimateGradient, PictureKeepOriginals, mode);
}
