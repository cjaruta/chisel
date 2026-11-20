using Chisel.Core.Clock;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Mapping;

public static class AlarmMapper
{
    public static AlarmEntity ToEntity(this Alarm alarm) => new()
    {
        Id = alarm.Id,
        Time = alarm.Time,
        Label = alarm.Label,
        Enabled = alarm.Enabled,
        Days = (int)alarm.Days,
        LastFiredOn = alarm.LastFiredOn
    };

    public static Alarm ToDomain(this AlarmEntity entity) =>
        new(entity.Id, entity.Time, entity.Label, entity.Enabled, (AlarmDays)entity.Days, entity.LastFiredOn);
}
