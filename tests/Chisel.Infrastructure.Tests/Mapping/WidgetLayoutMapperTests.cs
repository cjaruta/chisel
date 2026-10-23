using Chisel.Core.Layout;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;
using Xunit;

namespace Chisel.Infrastructure.Tests.Mapping;

public class WidgetLayoutMapperTests
{
    [Fact]
    public void ToEntities_AssignsColumnAndPositionFromEachList()
    {
        var layout = new WidgetLayout();
        layout.Move("clock", Column.Side, 0);
        layout.Move("tasks", Column.Side, 1);
        layout.Move("notes", Column.Main, 0);

        var entities = layout.ToEntities();

        Assert.Equal(3, entities.Count);

        var tasks = Assert.Single(entities, e => e.WidgetId == "tasks");
        Assert.Equal(Column.Side, tasks.Column);
        Assert.Equal(1, tasks.Position);

        var notes = Assert.Single(entities, e => e.WidgetId == "notes");
        Assert.Equal(Column.Main, notes.Column);
        Assert.Equal(0, notes.Position);
    }

    [Fact]
    public void ToLayout_RebuildsEachColumnInPositionOrder()
    {
        var entities = new[]
        {
            new WidgetPlacementEntity { Id = Guid.NewGuid(), WidgetId = "tasks", Column = Column.Side, Position = 1 },
            new WidgetPlacementEntity { Id = Guid.NewGuid(), WidgetId = "notes", Column = Column.Main, Position = 0 },
            new WidgetPlacementEntity { Id = Guid.NewGuid(), WidgetId = "clock", Column = Column.Side, Position = 0 }
        };

        var layout = entities.ToLayout();

        Assert.Equal(new[] { "clock", "tasks" }, layout.Side);
        Assert.Equal(new[] { "notes" }, layout.Main);
    }
}