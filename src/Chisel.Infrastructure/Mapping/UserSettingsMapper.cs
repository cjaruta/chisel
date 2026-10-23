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
        BedTime = settings.BedTime
    };

    public static UserSettings ToDomain(this UserSettingsEntity entity) =>
        new(entity.Name, entity.WakeTime, entity.BedTime);
}