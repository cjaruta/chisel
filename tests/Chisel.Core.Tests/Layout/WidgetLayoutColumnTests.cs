using Chisel.Core.Layout;
using Xunit;

namespace Chisel.Core.Tests.Layout;

// Making columns: moving a widget into a new column at a gap between columns (0 = left of the first,
// Count = right of the last), and removing columns left empty.
public class WidgetLayoutColumnTests
{
    private static readonly WidgetPlacement[] None = Array.Empty<WidgetPlacement>();

    private static WidgetLayout Layout(params LayoutColumn[] columns) => new(columns);

    private static LayoutColumn Col(double width, params string[] ids) =>
        new(width, ids.Select(id => new WidgetPlacement(id)).ToList());

    private static LayoutColumn Col(double width, params (string Id, double Height)[] widgets) =>
        new(width, widgets.Select(w => new WidgetPlacement(w.Id, w.Height)).ToList());

    private static string Order(WidgetLayout layout) =>
        string.Join(" | ", layout.Columns.Select(c => string.Join(",", c.Widgets.Select(w => w.WidgetId))));

    private static readonly WidgetLayout Default = Layout(
        Col(1.3, ("schedule", 1.0)),
        Col(1, ("tasks", 1.05), ("notes", 1.1), ("links", 0.85)));

    private static void AssertValid(WidgetLayout layout)
    {
        var ids = layout.WidgetIds.ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(new[] { "links", "notes", "schedule", "tasks" }, ids.OrderBy(x => x));
        Assert.NotEmpty(layout.Columns);
        Assert.True(layout.Columns.Count <= LayoutLimits.MaximumColumns);
        Assert.All(layout.Columns.Where(c => c.Widgets.Count > 0),
            c => Assert.Equal(c.Widgets.Count, c.Widgets.Sum(w => w.HeightWeight), 2));
    }

    [Fact]
    public void Limits_AreFourColumnsOfAtLeast260Pixels()
    {
        Assert.Equal(4, LayoutLimits.MaximumColumns);
        Assert.Equal(260, LayoutLimits.MinimumColumnWidth);
    }

    [Theory]
    [InlineData(0, "notes | schedule | tasks,links")]
    [InlineData(1, "schedule | notes | tasks,links")]
    [InlineData(2, "schedule | tasks,links | notes")]
    public void NewColumn_AtTheStartMiddleOrEnd(int index, string expected)
    {
        var moved = Default.MoveToNewColumn("notes", index);

        Assert.Equal(expected, Order(moved));
        AssertValid(moved);
    }

    [Fact]
    public void NewColumn_HoldsJustTheWidgetAtFullHeight_AndTheOldColumnIsRescaled()
    {
        var moved = Default.MoveToNewColumn("notes", 2);

        Assert.Equal("1.3[schedule:1] | 1[tasks:1.105, links:0.895] | 1.15[notes:1]", moved.Describe());
    }

    [Fact]
    public void NewColumn_GetsTheAverageWidth_AndTheOthersKeepTheirs()
    {
        var three = Default.MoveToNewColumn("notes", 2);
        var four = three.MoveToNewColumn("links", 0);

        Assert.Equal(new[] { 1.3, 1.0, 1.15 }, three.Columns.Select(c => c.WidthWeight));
        Assert.Equal(new[] { 1.15, 1.3, 1.0, 1.15 }, four.Columns.Select(c => c.WidthWeight));
    }

    [Fact]
    public void AtTheMaximum_ANewColumnIsRejected()
    {
        var four = Layout(Col(1, "schedule"), Col(1, "tasks"), Col(1, "notes", "links"), new LayoutColumn(1, None));

        Assert.Throws<InvalidOperationException>(() => four.MoveToNewColumn("notes", 4));
        Assert.False(four.CanMoveToNewColumn("notes"));
        Assert.True(Default.CanMoveToNewColumn("notes"));
    }

    [Fact]
    public void TheOnlyWidgetOfAColumn_RelocatesItsColumn_EvenAtTheMaximum()
    {
        var four = Layout(Col(2, "schedule"), Col(1, "tasks"), Col(1, "notes"), Col(1, "links"));

        Assert.True(four.CanMoveToNewColumn("schedule"));
        var moved = four.MoveToNewColumn("schedule", 3);

        Assert.Equal("tasks | notes | schedule | links", Order(moved));
        Assert.Equal(new[] { 1.0, 1.0, 2.0, 1.0 }, moved.Columns.Select(c => c.WidthWeight));
        AssertValid(moved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TheOnlyWidgetOfAColumn_NextToItself_IsANoOp(int index)
    {
        Assert.Same(Default, Default.MoveToNewColumn("schedule", index));
    }

    [Fact]
    public void ColumnIndexOutsideTheGaps_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Default.MoveToNewColumn("notes", 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Default.MoveToNewColumn("notes", -1));
    }

    [Fact]
    public void UnknownWidget_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Default.MoveToNewColumn("weather", 0));
        Assert.False(Default.CanMoveToNewColumn("weather"));
    }

    [Fact]
    public void MovingTheLastWidgetOut_LeavesAnEmptyColumn_ThatCleanupRemoves()
    {
        var emptied = Default.Move("schedule", 1, 0);
        Assert.Empty(emptied.Columns[0].Widgets);

        var cleaned = emptied.RemoveEmptyColumns();

        Assert.Equal("schedule,tasks,notes,links", Order(cleaned));
        Assert.Single(cleaned.Columns);
        Assert.Equal(1.0, cleaned.Columns[0].WidthWeight);
        AssertValid(cleaned);
    }

    [Fact]
    public void Cleanup_KeepsOrderAndRenormalizesHeights()
    {
        var layout = Layout(
            new LayoutColumn(1, None),
            Col(2, ("tasks", 2), ("notes", 6)),
            new LayoutColumn(3, None),
            Col(1, ("schedule", 5)),
            Col(1.5, ("links", 1)));

        var cleaned = layout.RemoveEmptyColumns();

        Assert.Equal("2[tasks:0.5, notes:1.5] | 1[schedule:1] | 1.5[links:1]", cleaned.Describe());
    }

    [Fact]
    public void Cleanup_WithNothingEmpty_KeepsTheSameLayout()
    {
        Assert.Same(Default, Default.RemoveEmptyColumns());
    }

    [Fact]
    public void EveryNewColumnMove_KeepsTheLayoutValid()
    {
        foreach (var id in new[] { "schedule", "tasks", "notes", "links" })
        {
            for (var index = 0; index <= Default.Columns.Count; index++)
            {
                var moved = Default.MoveToNewColumn(id, index);
                AssertValid(moved);
                AssertValid(moved.RemoveEmptyColumns());
            }
        }
    }
}
