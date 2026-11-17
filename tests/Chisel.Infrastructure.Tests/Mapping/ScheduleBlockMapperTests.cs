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

    [Fact]
    public void ToEntity_CopiesTheColorKey()
    {
        var block = new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(9, 0), "sage");

        Assert.Equal("sage", block.ToEntity().Color);
    }

    [Fact]
    public void ToDomain_CopiesTheColorKey()
    {
        var entity = new ScheduleBlockEntity { Title = "Focus", Start = new TimeOnly(8, 0), End = new TimeOnly(9, 0), Color = "lavender" };

        Assert.Equal("lavender", entity.ToDomain().Color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("neon")]
    public void ToDomain_MissingOrUnknownColor_IsTheDefault(string color)
    {
        var entity = new ScheduleBlockEntity { Title = "Focus", Start = new TimeOnly(8, 0), End = new TimeOnly(9, 0), Color = color };

        Assert.Equal(BlockColor.Default, entity.ToDomain().Color);
    }
}
