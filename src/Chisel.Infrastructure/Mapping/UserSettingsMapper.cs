using Chisel.Core.Settings;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Mapping;

public static class UserSettingsMapper
{
    public static UserSettingsEntity ToEntity(this UserSettings settings) => new()
    {
        Id = Guid.NewGuid(),
        Name = settings.Name,
        WakeTime = settings.WakeTime,
        BedTime = settings.BedTime,
        ClockFormat = (int)settings.ClockFormat,
        SchedulePalette = settings.SchedulePalette,
        TemperatureUnit = (int)settings.TemperatureUnit,
        MediaVisualizer = settings.MediaVisualizer,
        MediaVolume = settings.MediaVolume,
        MediaScrollVolume = settings.MediaScrollVolume,
        MediaAlbumGradient = settings.MediaAlbumGradient,
        MediaAnimateGradient = settings.MediaAnimateGradient,
        ThemeMode = settings.ThemeMode,
        AccentColor = settings.AccentColor
    };

    public static UserSettings ToDomain(this UserSettingsEntity entity) =>
        new(entity.Name, entity.WakeTime, entity.BedTime, (ClockFormat)entity.ClockFormat, entity.SchedulePalette, (TemperatureUnit)entity.TemperatureUnit, entity.MediaVisualizer, entity.MediaVolume, entity.MediaScrollVolume, entity.ThemeMode, entity.AccentColor, entity.MediaAlbumGradient, entity.MediaAnimateGradient);
}