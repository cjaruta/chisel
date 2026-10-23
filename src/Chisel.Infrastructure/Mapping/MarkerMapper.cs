using Chisel.Core.Schedule;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Mapping;

public static class MarkerMapper
{
    public static MarkerEntity ToEntity(this Marker marker) => new()
    {
        Id = Guid.NewGuid(),
        Label = marker.Label,
        Time = marker.Time,
        Kind = marker.Kind
    };

    public static Marker ToDomain(this MarkerEntity entity) =>
        new(entity.Label, entity.Time, entity.Kind);
}