using Chisel.Core.Layout;
using Xunit;

namespace Chisel.Core.Tests.Layout;

// Removing a widget hides it (its data is untouched); adding brings a hidden widget back into a column gap
// or into a new column. Columns stay filled: heights are rescaled to average 1 after either.
public class WidgetLayoutAddRemoveTests
{
    private static WidgetLayout Layout(params LayoutColumn[] columns) => new(columns);

    private static LayoutColumn Col(double width, params (string Id, double Height)[] widgets) =>
        new(width, widgets.Select(w => new WidgetPlacement(w.Id, w.Height)).ToList());

    private static string Order(WidgetLayout layout) =>
        string.Join(" | ", layout.Columns.Select(c => string.Join(",", c.Widgets.Select(w => w.WidgetId))))
        + (layout.HiddenWidgetIds.Count > 0 ? $" (hidden: {string.Join(",", layout.HiddenWidgetIds)})" : "");

    private static readonly WidgetLayout Default = Layout(
        Col(1.3, ("schedule", 1.0)),
        Col(1, ("tasks", 1.05), ("notes", 1.1), ("links", 0.85)));

    private static readonly WidgetLayout NotesHidden = Default.RemoveWidget("notes");

    private static void AssertValid(WidgetLayout layout)
    {
        var placed = layout.WidgetIds.ToList();
        var all = placed.Concat(layout.HiddenWidgetIds).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.NotEmpty(placed);
        Assert.NotEmpty(layout.Columns);
        Assert.All(layout.Columns, c => Assert.NotEmpty(c.Widgets));
        Assert.All(layout.Columns, c => Assert.Equal(c.Widgets.Count, c.Widgets.Sum(w => w.HeightWeight), 2));
    }

    [Fact]
    public void Remove_HidesTheWidgetAndRescalesItsColumn()
    {
        Assert.Equal("1.3[schedule:1] | 1[tasks:1.105, links:0.895] (hidden: notes)", NotesHidden.Describe());
        AssertValid(NotesHidden);
    }

    [Fact]
    public void Remove_TheOnlyWidgetOfAColumn_CollapsesTheColumn()
    {
        var removed = Default.RemoveWidget("schedule");

        Assert.Equal("tasks,notes,links (hidden: schedule)", Order(removed));
        Assert.Equal(1.0, removed.Columns[0].WidthWeight);
        AssertValid(removed);
    }

    [Fact]
    public void Remove_TheLastWidget_IsRejected()
    {
        var one = Default.RemoveWidget("schedule").RemoveWidget("tasks").RemoveWidget("notes");

        Assert.False(one.CanRemoveWidget);
        Assert.True(Default.CanRemoveWidget);
        Assert.Throws<InvalidOperationException>(() => one.RemoveWidget("links"));
    }

    [Fact]
    public void Remove_AWidgetThatIsNotPlaced_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => NotesHidden.RemoveWidget("notes"));
        Assert.Throws<ArgumentException>(() => Default.RemoveWidget("weather"));
    }

    [Theory]
    [InlineData(0, "schedule | notes,tasks,links")]
    [InlineData(1, "schedule | tasks,notes,links")]
    [InlineData(2, "schedule | tasks,links,notes")]
    public void Add_AtTheStartMiddleOrEndOfAColumn(int index, string expected)
    {
        var added = NotesHidden.AddWidget("notes", 1, index, 1.1);

        Assert.Equal(expected, Order(added));
        Assert.Empty(added.HiddenWidgetIds);
        AssertValid(added);
    }

    [Fact]
    public void Add_GivesTheWidgetItsDefaultHeightThenRescalesTheColumn()
    {
        var added = NotesHidden.AddWidget("notes", 0, 1, 2);

        // Schedule 1 + notes 2 = 3, rescaled to 2.
        Assert.Equal("1.3[schedule:0.667, notes:1.333] | 1[tasks:1.105, links:0.895]", added.Describe());
    }

    [Fact]
    public void Add_IndexPastTheEnd_GoesLast()
    {
        Assert.Equal("schedule,notes | tasks,links", Order(NotesHidden.AddWidget("notes", 0, 99, 1)));
    }

    [Fact]
    public void Add_IntoANewColumn_AtAnyGap()
    {
        Assert.Equal("notes | schedule | tasks,links", Order(NotesHidden.AddWidgetInNewColumn("notes", 0)));
        Assert.Equal("schedule | notes | tasks,links", Order(NotesHidden.AddWidgetInNewColumn("notes", 1)));

        var right = NotesHidden.AddWidgetInNewColumn("notes", 2);
        Assert.Equal("1.3[schedule:1] | 1[tasks:1.105, links:0.895] | 1.15[notes:1]", right.Describe());
        AssertValid(right);
    }

    [Fact]
    public void Add_IntoANewColumn_RespectsTheMaximum()
    {
        var four = new WidgetLayout(
            new[] { Col(1, ("a", 1)), Col(1, ("b", 1)), Col(1, ("c", 1)), Col(1, ("d", 1)) },
            new[] { "e" });

        Assert.False(four.CanAddInNewColumn);
        Assert.True(NotesHidden.CanAddInNewColumn);
        Assert.Throws<InvalidOperationException>(() => four.AddWidgetInNewColumn("e", 4));
        Assert.Equal("a,e | b | c | d", Order(four.AddWidget("e", 0, 1, 1)));
    }

    [Fact]
    public void Add_AWidgetThatIsAlreadyPlaced_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Default.AddWidget("tasks", 0, 0, 1));
        Assert.Throws<ArgumentException>(() => Default.AddWidgetInNewColumn("tasks", 0));
    }

    [Fact]
    public void Add_AnUnknownWidget_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => NotesHidden.AddWidget("weather", 0, 0, 1));
        Assert.Throws<ArgumentException>(() => NotesHidden.AddWidgetInNewColumn("weather", 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Add_IntoAColumnThatDoesNotExist_IsRejected(int column)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NotesHidden.AddWidget("notes", column, 0, 1));
    }

    [Fact]
    public void RemoveThenAdd_RoundTripsTheWidgets()
    {
        var layout = Default.RemoveWidget("links").RemoveWidget("schedule").AddWidget("links", 0, 0, 0.85).AddWidgetInNewColumn("schedule", 0);

        Assert.Equal("schedule | links,tasks,notes", Order(layout));
        AssertValid(layout);
    }

    [Fact]
    public void OtherOperations_KeepTheHiddenWidgets()
    {
        Assert.Equal(new[] { "notes" }, NotesHidden.Move("tasks", 0, 1).HiddenWidgetIds);
        Assert.Equal(new[] { "notes" }, NotesHidden.MoveToNewColumn("tasks", 0).HiddenWidgetIds);
        Assert.Equal(new[] { "notes" }, NotesHidden.Resize("tasks", 0.05, 0.1).HiddenWidgetIds);
        Assert.Equal(new[] { "notes" }, NotesHidden.Move("schedule", 1, 0).RemoveEmptyColumns().HiddenWidgetIds);
    }
}
