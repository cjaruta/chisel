using Chisel.Core.Layout;
using Xunit;

namespace Chisel.Core.Tests.Layout;

// Moving a widget to a gap in a column: index is the gap as seen before the move (0 = top, Count = bottom).
// Columns are never added or removed; the columns it leaves and joins get their height weights rescaled
// so they average 1, keeping the other widgets' proportions.
public class WidgetLayoutMoveTests
{
    private static WidgetLayout Layout(params LayoutColumn[] columns) => new(columns);

    private static LayoutColumn Col(double width, params string[] ids) =>
        new(width, ids.Select(id => new WidgetPlacement(id)).ToList());

    private static LayoutColumn Col(double width, params (string Id, double Height)[] widgets) =>
        new(width, widgets.Select(w => new WidgetPlacement(w.Id, w.Height)).ToList());

    private static string Order(WidgetLayout layout) =>
        string.Join(" | ", layout.Columns.Select(c => string.Join(",", c.Widgets.Select(w => w.WidgetId))));

    private static readonly WidgetLayout Sample = Layout(Col(1.3, "schedule"), Col(1, "tasks", "notes", "links"));

    [Fact]
    public void ReorderUp_WithinAColumn()
    {
        Assert.Equal("schedule | links,tasks,notes", Order(Sample.Move("links", 1, 0)));
        Assert.Equal("schedule | tasks,links,notes", Order(Sample.Move("links", 1, 1)));
    }

    [Fact]
    public void ReorderDown_WithinAColumn_AdjustsForTheWidgetsOwnGap()
    {
        // Gap 2 is between notes and links; gap 3 is the bottom.
        Assert.Equal("schedule | notes,tasks,links", Order(Sample.Move("tasks", 1, 2)));
        Assert.Equal("schedule | notes,links,tasks", Order(Sample.Move("tasks", 1, 3)));
    }

    [Theory]
    [InlineData("tasks", 1, 0)]
    [InlineData("tasks", 1, 1)]
    [InlineData("notes", 1, 1)]
    [InlineData("notes", 1, 2)]
    [InlineData("schedule", 0, 0)]
    [InlineData("schedule", 0, 1)]
    public void OntoItsOwnPosition_IsANoOp(string id, int column, int index)
    {
        Assert.Same(Sample, Sample.Move(id, column, index));
    }

    [Theory]
    [InlineData(0, "notes,schedule | tasks,links")]
    [InlineData(1, "schedule,notes | tasks,links")]
    public void ToAnotherColumn_AtTheStartOrEnd(int index, string expected)
    {
        Assert.Equal(expected, Order(Sample.Move("notes", 0, index)));
    }

    [Fact]
    public void ToAnotherColumn_InTheMiddle()
    {
        var layout = Layout(Col(1, "schedule", "tasks"), Col(1, "notes", "links"));

        Assert.Equal("schedule,links,tasks | notes", Order(layout.Move("links", 0, 1)));
    }

    [Fact]
    public void IndexPastTheEnd_GoesLast()
    {
        Assert.Equal("schedule,tasks | notes,links", Order(Sample.Move("tasks", 0, 99)));
    }

    [Fact]
    public void IntoAnEmptyColumn()
    {
        var layout = Layout(Col(1, "schedule", "tasks", "notes", "links"), new LayoutColumn(1, Array.Empty<WidgetPlacement>()));

        Assert.Equal("schedule,tasks,links | notes", Order(layout.Move("notes", 1, 0)));
    }

    [Fact]
    public void OutOfAColumn_LeavesItEmptyButKeepsIt()
    {
        var moved = Sample.Move("schedule", 1, 3);

        Assert.Equal(" | tasks,notes,links,schedule", Order(moved));
        Assert.Equal(2, moved.Columns.Count);
        Assert.Equal(1.3, moved.Columns[0].WidthWeight);
        Assert.Empty(moved.Columns[0].Widgets);
    }

    [Fact]
    public void ColumnWidthsAreUnchanged()
    {
        Assert.Equal(new[] { 1.3, 1.0 }, Sample.Move("tasks", 0, 1).Columns.Select(c => c.WidthWeight));
    }

    [Fact]
    public void BetweenColumns_TheWidgetKeepsItsHeightAndBothColumnsAreRescaledToAverageOne()
    {
        var layout = Layout(Col(1, ("schedule", 2)), Col(1, ("tasks", 1), ("notes", 3), ("links", 4)));

        var moved = layout.Move("links", 0, 1);

        // Joined: schedule 2, links 4 -> sum 6, rescaled to sum 2 keeps 1:2.
        Assert.Equal("1[schedule:0.667, links:1.333] | 1[tasks:0.5, notes:1.5]", moved.Describe());
    }

    [Fact]
    public void WithinAColumn_WeightsMoveWithTheirWidgetsAndAverageOne()
    {
        var layout = Layout(Col(1, ("schedule", 1)), Col(1, ("tasks", 1), ("notes", 2), ("links", 3)));

        Assert.Equal("1[schedule:1] | 1[links:1.5, tasks:0.5, notes:1]", layout.Move("links", 1, 0).Describe());
    }

    [Fact]
    public void EveryWidgetAppearsExactlyOnceAfterAnyMove()
    {
        var all = new[] { "schedule", "tasks", "notes", "links" };
        foreach (var id in all)
        {
            for (var column = 0; column < Sample.Columns.Count; column++)
            {
                for (var index = 0; index <= 4; index++)
                {
                    var moved = Sample.Move(id, column, index);
                    Assert.Equal(all.OrderBy(x => x), moved.WidgetIds.OrderBy(x => x));
                    Assert.Equal(2, moved.Columns.Count);
                    Assert.All(moved.Columns.SelectMany(c => c.Widgets), w => Assert.True(w.HeightWeight >= WidgetLayout.MinimumWeight));
                }
            }
        }
    }

    [Fact]
    public void UnknownWidget_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Sample.Move("weather", 1, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void ColumnThatDoesNotExist_IsRejected(int column)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Sample.Move("tasks", column, 0));
    }

    [Fact]
    public void NormalizeHeights_RescalesToAverageOneAndKeepsProportions()
    {
        var column = new LayoutColumn(1, new[] { new WidgetPlacement("a", 1), new WidgetPlacement("b", 3) });

        var normalized = WidgetLayout.NormalizeHeights(column);

        Assert.Equal(new[] { 0.5, 1.5 }, normalized.Widgets.Select(w => w.HeightWeight));
        Assert.Empty(WidgetLayout.NormalizeHeights(new LayoutColumn(1, Array.Empty<WidgetPlacement>())).Widgets);
    }
}
