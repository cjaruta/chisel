using Chisel.Core.Schedule;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Mapping;

public static class ScheduleBlockMapper
{
    public static ScheduleBlockEntity ToEntity(this ScheduleBlock block) => new()
    {
        Id = Guid.NewGuid(),
        Title = block.Title,
        Start = block.Start,
        End = block.End
    };

    public static ScheduleBlock ToDomain(this ScheduleBlockEntity entity) =>
        new(entity.Title, entity.Start, entity.End);
}