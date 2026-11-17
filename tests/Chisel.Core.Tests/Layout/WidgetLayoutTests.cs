using Chisel.Core.Layout;
using Xunit;

namespace Chisel.Core.Tests.Layout;

// A layout is an ordered list of columns (each with a width weight) holding ordered widget placements
// (each with a height weight). Layouts are immutable; Move and Remove return a new one.
public class WidgetLayoutTests
{
    private static WidgetLayout Layout(params LayoutColumn[] columns) => new(columns);

    private static LayoutColumn Col(double width, params string[] ids) =>
        new(width, ids.Select(id => new WidgetPlacement(id)).ToList());

    [Fact]
    public void Describe_ShowsColumnsWidthsAndHeights()
    {
        var layout = Layout(
            new LayoutColumn(1.3, new[] { new WidgetPlacement("schedule") }),
            new LayoutColumn(1, new[] { new WidgetPlacement("tasks", 2), new WidgetPlacement("notes", 0.5) }));

        Assert.Equal("1.3[schedule:1] | 1[tasks:2, notes:0.5]", layout.Describe());
    }

    [Fact]
    public void WidgetIds_ListsEveryPlacedWidgetInOrder()
    {
        var layout = Layout(Col(1, "schedule"), Col(1, "tasks", "notes"));

        Assert.Equal(new[] { "schedule", "tasks", "notes" }, layout.WidgetIds);
    }

    [Fact]
    public void Find_ReturnsColumnAndIndex()
    {
        var layout = Layout(Col(1, "schedule"), Col(1, "tasks", "notes"));

        Assert.Equal((1, 1), layout.Find("notes"));
        Assert.Null(layout.Find("clock"));
    }

    [Fact]
    public void Move_ToAnotherColumn_TakesItOutOfItsOldPlace()
    {
        var layout = Layout(Col(1, "schedule", "links"), Col(1, "tasks", "notes"));

        var moved = layout.Move("links", 1, 1);

        Assert.Equal("1[schedule:1] | 1[tasks:1, links:1, notes:1]", moved.Describe());
        Assert.Equal("1[schedule:1, links:1] | 1[tasks:1, notes:1]", layout.Describe());
    }

    [Fact]
    public void Move_KeepsTheWidgetsHeightWeight()
    {
        var layout = Layout(new LayoutColumn(1, new[] { new WidgetPlacement("tasks", 2.5) }), Col(1, "notes"));

        Assert.Equal("1[notes:1, tasks:2.5]", layout.Move("tasks", 1, 1).Describe());
    }

    [Fact]
    public void Move_PastTheLastColumn_StartsANewColumn()
    {
        var layout = Layout(Col(1, "schedule", "tasks"));

        Assert.Equal("1[schedule:1] | 1[tasks:1]", layout.Move("tasks", 5, 0).Describe());
    }

    [Fact]
    public void Move_LeavingAColumnEmpty_DropsThatColumn()
    {
        var layout = Layout(Col(1, "schedule"), Col(1, "tasks"));

        Assert.Equal("1[tasks:1, schedule:1]", layout.Move("schedule", 1, 1).Describe());
    }

    [Fact]
    public void Remove_TakesTheWidgetOutAndDropsAnEmptiedColumn()
    {
        var layout = Layout(Col(1.3, "schedule"), Col(1, "tasks", "notes"));

        Assert.Equal("1[tasks:1, notes:1]", layout.Remove("schedule").Describe());
        Assert.Equal("1.3[schedule:1] | 1[notes:1]", layout.Remove("tasks").Describe());
    }
}
