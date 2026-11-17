using Chisel.Core.Layout;
using Xunit;

namespace Chisel.Core.Tests.Layout;

// A layout is an ordered list of columns (each with a width weight) holding ordered widget placements
// (each with a height weight). Layouts are immutable; Move and Remove return a new one.
// Moving is covered in WidgetLayoutMoveTests.
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
    public void Describe_ListsHiddenWidgetsAtTheEnd()
    {
        var layout = new WidgetLayout(new[] { Col(1, "schedule") }, new[] { "tasks", "notes" });

        Assert.Equal("1[schedule:1] (hidden: tasks, notes)", layout.Describe());
        Assert.Equal(new[] { "tasks", "notes" }, layout.HiddenWidgetIds);
    }
}
