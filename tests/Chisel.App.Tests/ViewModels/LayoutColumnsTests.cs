using Chisel.App.ViewModels;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.ViewModels;

// New columns in layout edit mode: dropping a widget on a "New column" zone, when such a zone is offered,
// and Done tidying away empty columns so what is saved is what is shown.
public class LayoutColumnsTests
{
    private sealed class FakeLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; set; } = TestLayouts.Classic;
        public int SaveCalls { get; private set; }
        public WidgetLayout? Get() => Saved;
        public void Save(WidgetLayout layout)
        {
            Saved = layout;
            SaveCalls++;
        }
    }

    private const double Gap = 16;

    private readonly FakeLayoutRepository _repository = new();

    private static string Order(WidgetLayout layout) =>
        string.Join(" | ", layout.Columns.Select(c => string.Join(",", c.Widgets.Select(w => w.WidgetId))));

    private LayoutViewModel Editing()
    {
        var layout = new LayoutViewModel(_repository);
        layout.EnterEditCommand.Execute(null);
        return layout;
    }

    [Fact]
    public void DropInANewColumn_ChangesTheWorkingCopyOnly()
    {
        var layout = Editing();
        var saves = _repository.SaveCalls;
        layout.BeginWidgetDrag("notes");

        Assert.True(layout.DropWidgetInNewColumn(2));

        Assert.Equal("schedule | tasks,scripture | notes", Order(layout.Layout));
        Assert.False(layout.IsDraggingWidget);
        Assert.Equal(TestLayouts.Classic.Describe(), layout.Snapshot!.Describe());
        Assert.Equal(saves, _repository.SaveCalls);
    }

    [Fact]
    public void DropInANewColumn_WithoutADrag_DoesNothing()
    {
        var layout = Editing();

        Assert.False(layout.DropWidgetInNewColumn(0));

        Assert.Equal(TestLayouts.Classic.Describe(), layout.Layout.Describe());
    }

    [Fact]
    public void Done_AfterAColumnCollapsed_SavesWhatIsShown_AndThatIsWhatLoads()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("schedule");
        layout.DropWidget(1, 0);

        layout.DoneCommand.Execute(null);

        Assert.Equal("schedule,tasks,notes,scripture", Order(layout.Layout));
        Assert.Equal(layout.Layout.Describe(), _repository.Saved!.Describe());
        Assert.Equal(layout.Layout.Describe(), new LayoutViewModel(_repository).Layout.Describe());
    }

    [Fact]
    public void Done_WithThreeColumns_PersistsThem()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("scripture");
        layout.DropWidgetInNewColumn(0);

        layout.DoneCommand.Execute(null);

        Assert.Equal("scripture | schedule | tasks,notes", Order(_repository.Saved!));
        Assert.Equal("scripture | schedule | tasks,notes", Order(new LayoutViewModel(_repository).Layout));
    }

    [Fact]
    public void Cancel_RestoresTheSnapshotsColumns()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("scripture");
        layout.DropWidgetInNewColumn(2);
        layout.BeginWidgetDrag("tasks");
        layout.DropWidgetInNewColumn(0);
        Assert.Equal(4, layout.Layout.Columns.Count);

        layout.CancelCommand.Execute(null);

        Assert.Equal(TestLayouts.Classic.Describe(), layout.Layout.Describe());
    }

    [Fact]
    public void Reset_RestoresTheDefaultTwoColumns()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("scripture");
        layout.DropWidgetInNewColumn(2);

        layout.ResetCommand.Execute(null);

        Assert.Equal(2, layout.Layout.Columns.Count);
        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Layout.Describe());
    }

    [Fact]
    public void NewColumnZones_AreOfferedOnlyWhileDragging()
    {
        var layout = Editing();
        Assert.False(layout.CanOfferNewColumn(2000, Gap));

        layout.BeginWidgetDrag("notes");

        Assert.True(layout.CanOfferNewColumn(2000, Gap));
    }

    [Fact]
    public void NewColumnZones_AreNotOfferedWhenTheWindowIsTooNarrow()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("notes");

        // Three columns need 3 * 260 + 2 * 16 = 812 pixels.
        Assert.True(layout.CanOfferNewColumn(812, Gap));
        Assert.False(layout.CanOfferNewColumn(811, Gap));
    }

    // With four widgets, four columns means every widget is alone, so at the maximum a "New column" drop can
    // only move a column, never add one. (The hard limit itself is covered in the Core tests.)
    [Fact]
    public void AtTheMaximum_ANewColumnDropOnlyMovesAColumn()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("tasks");
        layout.DropWidgetInNewColumn(2);
        layout.BeginWidgetDrag("notes");
        layout.DropWidgetInNewColumn(3);
        Assert.Equal(LayoutLimits.MaximumColumns, layout.Layout.Columns.Count);

        layout.BeginWidgetDrag("scripture");
        Assert.True(layout.CanOfferNewColumn(1100, Gap));
        layout.DropWidgetInNewColumn(0);

        Assert.Equal(LayoutLimits.MaximumColumns, layout.Layout.Columns.Count);
        Assert.Equal("scripture | schedule | tasks | notes", Order(layout.Layout));
    }

    [Fact]
    public void AWidgetAloneInItsColumn_CanAlwaysMoveItsColumn()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("tasks");
        layout.DropWidgetInNewColumn(2);
        layout.BeginWidgetDrag("notes");
        layout.DropWidgetInNewColumn(3);

        layout.BeginWidgetDrag("schedule");

        Assert.True(layout.CanOfferNewColumn(1100, Gap));
        Assert.True(layout.DropWidgetInNewColumn(4));
        Assert.Equal("scripture | tasks | notes | schedule", Order(layout.Layout));
    }
}
