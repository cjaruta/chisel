using Chisel.App.ViewModels;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.ViewModels;

// Removing widgets to the tray and adding them back, in layout edit mode. All of it changes only the
// working copy until Done.
public class LayoutTrayTests
{
    private sealed class FakeLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; set; }
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
        string.Join(" | ", layout.Columns.Select(c => string.Join(",", c.Widgets.Select(w => w.WidgetId))))
        + (layout.HiddenWidgetIds.Count > 0 ? $" (hidden: {string.Join(",", layout.HiddenWidgetIds)})" : "");

    private static string[] Tray(LayoutViewModel layout) => layout.TrayWidgets.Select(w => w.Id).ToArray();

    private LayoutViewModel Editing()
    {
        var layout = new LayoutViewModel(_repository);
        layout.EnterEditCommand.Execute(null);
        return layout;
    }

    [Fact]
    public void Registry_GivesEveryWidgetAnIconAndTheDefaultLayoutsHeight()
    {
        Assert.All(WidgetRegistry.All, w => Assert.False(string.IsNullOrWhiteSpace(w.Icon), w.Id));
        var defaults = WidgetRegistry.DefaultLayout.Columns.SelectMany(c => c.Widgets).ToDictionary(p => p.WidgetId, p => p.HeightWeight);
        Assert.All(WidgetRegistry.All, w => Assert.Equal(w.DefaultHeight, defaults[w.Id]));
        Assert.Equal(WidgetRegistry.All.Select(w => w.Id).OrderBy(x => x), defaults.Keys.OrderBy(x => x));
    }

    [Fact]
    public void Remove_HidesTheWidgetInTheWorkingCopyOnly()
    {
        var layout = Editing();
        var saves = _repository.SaveCalls;

        layout.RemoveWidget("notes");

        Assert.Equal("schedule | tasks,links (hidden: notes)", Order(layout.Layout));
        Assert.Equal(new[] { "notes" }, Tray(layout));
        Assert.Equal(saves, _repository.SaveCalls);
    }

    [Fact]
    public void Remove_OnlyInEditMode()
    {
        var layout = new LayoutViewModel(_repository);

        layout.RemoveWidget("notes");

        Assert.Empty(Tray(layout));
    }

    [Fact]
    public void Remove_TheOnlyWidgetOfAColumn_CollapsesTheColumn()
    {
        var layout = Editing();

        layout.RemoveWidget("schedule");

        Assert.Equal("tasks,notes,links (hidden: schedule)", Order(layout.Layout));
    }

    [Fact]
    public void TheLastWidget_CannotBeRemoved()
    {
        var layout = Editing();
        layout.RemoveWidget("schedule");
        layout.RemoveWidget("tasks");
        Assert.True(layout.CanRemoveWidgets);
        layout.RemoveWidget("notes");

        Assert.False(layout.CanRemoveWidgets);
        layout.RemoveWidget("links");

        Assert.Equal("links (hidden: schedule,tasks,notes)", Order(layout.Layout));
    }

    [Fact]
    public void Tray_ListsExactlyTheHiddenWidgetsInRegistryOrder()
    {
        var layout = Editing();
        Assert.Empty(Tray(layout));

        layout.RemoveWidget("links");
        layout.RemoveWidget("schedule");

        Assert.Equal(new[] { "schedule", "links" }, Tray(layout));
        Assert.Equal(new[] { "Today", "Quick links" }, layout.TrayWidgets.Select(w => w.Title));
        Assert.True(layout.HasHiddenWidgets);
    }

    [Fact]
    public void Tray_OpensWhenAWidgetIsRemoved_AndOnEnteringEditModeWithWidgetsHidden()
    {
        var layout = Editing();
        Assert.False(layout.IsTrayOpen);

        layout.RemoveWidget("notes");
        Assert.True(layout.IsTrayOpen);

        layout.ToggleTrayCommand.Execute(null);
        Assert.False(layout.IsTrayOpen);
        layout.DoneCommand.Execute(null);

        layout.EnterEditCommand.Execute(null);
        Assert.True(layout.IsTrayOpen);
    }

    [Fact]
    public void ClickingATrayCard_AddsItAtTheBottomOfTheFirstColumn()
    {
        var layout = Editing();
        layout.RemoveWidget("notes");

        layout.AddWidgetFromTray("notes");

        Assert.Equal("schedule,notes | tasks,links", Order(layout.Layout));
        Assert.Equal(2, layout.Layout.Columns[0].Widgets.Sum(w => w.HeightWeight), 2);
        Assert.Empty(Tray(layout));
    }

    [Fact]
    public void DraggingFromTheTray_DropsIntoAColumnGap()
    {
        var layout = Editing();
        layout.RemoveWidget("notes");

        layout.BeginTrayDrag("notes");
        Assert.True(layout.IsDraggingWidget);
        Assert.True(layout.IsDraggingFromTray);
        Assert.True(layout.DropWidget(1, 0));

        Assert.Equal("schedule | notes,tasks,links", Order(layout.Layout));
        Assert.False(layout.IsDraggingWidget);
        Assert.False(layout.IsDraggingFromTray);
    }

    [Fact]
    public void DraggingFromTheTray_DropsIntoANewColumn()
    {
        var layout = Editing();
        layout.RemoveWidget("notes");
        layout.BeginTrayDrag("notes");

        Assert.True(layout.CanOfferNewColumn(2000, Gap));
        Assert.True(layout.DropWidgetInNewColumn(2));

        Assert.Equal("schedule | tasks,links | notes", Order(layout.Layout));
    }

    [Fact]
    public void DraggingFromTheTray_NoNewColumnWhenTooNarrow()
    {
        var layout = Editing();
        layout.RemoveWidget("notes");
        layout.BeginTrayDrag("notes");

        Assert.False(layout.CanOfferNewColumn(811, Gap));
    }

    [Fact]
    public void ACancelledOrInvalidTrayDrag_ChangesNothing()
    {
        var layout = Editing();
        layout.RemoveWidget("notes");
        var before = layout.Layout;

        layout.BeginTrayDrag("notes");
        layout.CancelWidgetDrag();
        layout.BeginTrayDrag("notes");
        layout.DropWidget(9, 0);

        Assert.Same(before, layout.Layout);
        Assert.False(layout.IsDraggingFromTray);
    }

    [Fact]
    public void BeginTrayDrag_ForAWidgetThatIsNotHidden_DoesNothing()
    {
        var layout = Editing();

        layout.BeginTrayDrag("notes");

        Assert.False(layout.IsDraggingWidget);
    }

    [Fact]
    public void Done_PersistsTheHiddenWidgets_AndTheyStayHiddenAfterARestart()
    {
        var layout = Editing();
        layout.RemoveWidget("notes");

        layout.DoneCommand.Execute(null);

        Assert.Equal(new[] { "notes" }, _repository.Saved!.HiddenWidgetIds);
        var restarted = new LayoutViewModel(_repository);
        Assert.Equal("schedule | tasks,links (hidden: notes)", Order(restarted.Layout));
        Assert.Equal(new[] { "notes" }, Tray(restarted));
    }

    [Fact]
    public void Cancel_RestoresTheRemovedWidgets()
    {
        var layout = Editing();
        layout.RemoveWidget("notes");
        layout.RemoveWidget("schedule");

        layout.CancelCommand.Execute(null);

        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Layout.Describe());
        Assert.Empty(Tray(layout));
    }

    [Fact]
    public void Reset_PutsEveryDefaultWidgetBack()
    {
        var layout = Editing();
        layout.RemoveWidget("notes");
        layout.RemoveWidget("links");

        layout.ResetCommand.Execute(null);

        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Layout.Describe());
        Assert.Empty(Tray(layout));
        Assert.False(layout.HasHiddenWidgets);
    }
}
