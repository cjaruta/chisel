using Chisel.Core.Schedule;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;
using Xunit;

namespace Chisel.Infrastructure.Tests.Mapping;

public class MarkerMapperTests
{
    [Fact]
    public void ToEntity_CopiesLabelTimeAndKind()
    {
        var marker = new Marker("Last coffee", new TimeOnly(13, 0), MarkerKind.Reminder);

        var entity = marker.ToEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("Last coffee", entity.Label);
        Assert.Equal(new TimeOnly(13, 0), entity.Time);
        Assert.Equal(MarkerKind.Reminder, entity.Kind);
    }

    [Fact]
    public void ToDomain_CopiesLabelTimeAndKind()
    {
        var entity = new MarkerEntity
        {
            Id = Guid.NewGuid(),
            Label = "All work done",
            Time = new TimeOnly(18, 0),
            Kind = MarkerKind.Checkpoint
        };

        var marker = entity.ToDomain();

        Assert.Equal("All work done", marker.Label);
        Assert.Equal(new TimeOnly(18, 0), marker.Time);
        Assert.Equal(MarkerKind.Checkpoint, marker.Kind);
    }
}