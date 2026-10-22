using Chisel.Core.Schedule;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;
using Xunit;

namespace Chisel.Infrastructure.Tests.Mapping;

public class ScheduleBlockMapperTests
{
    [Fact]
    public void ToEntity_CopiesTitleStartAndEnd()
    {
        var block = new ScheduleBlock("Focused work", new TimeOnly(8, 0), new TimeOnly(11, 0));

        var entity = block.ToEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("Focused work", entity.Title);
        Assert.Equal(new TimeOnly(8, 0), entity.Start);
        Assert.Equal(new TimeOnly(11, 0), entity.End);
    }

    [Fact]
    public void ToDomain_CopiesTitleStartAndEnd()
    {
        var entity = new ScheduleBlockEntity
        {
            Id = Guid.NewGuid(),
            Title = "Focused work",
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(11, 0)
        };

        var block = entity.ToDomain();

        Assert.Equal("Focused work", block.Title);
        Assert.Equal(new TimeOnly(8, 0), block.Start);
        Assert.Equal(new TimeOnly(11, 0), block.End);
    }
}