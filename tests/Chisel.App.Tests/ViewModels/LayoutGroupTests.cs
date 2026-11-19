using Chisel.App.ViewModels;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.ViewModels;

// Tabbed groups in the layout view model: merging by drag, tab switching, unmerge, reorder, the remembered
// header choice, and edit mode's Done / Cancel / Reset on a whole layout that holds groups.
public class LayoutGroupTests
{
    private sealed class FakeLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; set; }
        public int Saves { get; private set; }
        public WidgetLayout? Get() => Saved;
        public void Save(WidgetLayout layout) { Saved = layout; Saves++; }
    }

    private readonly FakeLayoutRepository _repository = new() { Saved = WidgetRegistry.DefaultLayout };

    private LayoutViewModel Editing()
    {
        var layout = new LayoutViewModel(_repository);
        layout.EnterEditCommand.Execute(null);
        return layout;
    }

    private static void Merge(LayoutViewModel layout, string dragged, string target)
    {
        layout.BeginWidgetDrag(dragged);
        Assert.True(layout.CanMergeInto(target));
        Assert.True(layout.DropOnWidget(target));
    }

    private static WidgetSlot Slot(LayoutViewModel layout, string id) =>
        layout.Columns.SelectMany(c => c.Widgets).Single(w => w.Members.Any(m => m.Id == id));

    [Fact]
    public void Dropping_OnAnotherWidget_MakesATabbedGroup_WithTheTargetActive()
    {
        var layout = Editing();

        Merge(layout, "notes", "tasks");

        var slot = Slot(layout, "notes");
        Assert.True(slot.IsGroup);
        Assert.Equal(new[] { "tasks", "notes" }, slot.Members.Select(m => m.Id));
        Assert.Equal("tasks", slot.ActiveWidget.Id);
        Assert.False(layout.IsDraggingWidget);
    }

    [Fact]
    public void ADrop_OnTheWidgetBeingDragged_IsNotAMerge()
    {
        var layout = Editing();
        layout.BeginWidgetDrag("notes");

        Assert.False(layout.CanMergeInto("notes"));
        Assert.False(layout.DropOnWidget("notes"));
        Assert.False(layout.IsDraggingWidget);
    }

    [Fact]
    public void ADrop_FromTheTray_IsNotAMerge()
    {
        var layout = Editing();
        layout.BeginTrayDrag(WidgetRegistry.Clock);

        Assert.False(layout.CanMergeInto("tasks"));
    }

    [Fact]
    public void AGroupOfFive_RefusesASixth()
    {
        var layout = Editing();
        foreach (var id in new[] { WidgetRegistry.Clock, WidgetRegistry.Weather, WidgetRegistry.Media })
        {
            layout.AddWidgetFromTray(id);
        }
        foreach (var id in new[] { "notes", "links", "schedule", "clock" })
        {
            Merge(layout, id, "tasks");
        }
        layout.BeginWidgetDrag(WidgetRegistry.Weather);

        Assert.Equal(LayoutLimits.MaximumGroupMembers, Slot(layout, "tasks").Members.Count);
        Assert.False(layout.CanMergeInto("tasks"));
        Assert.False(layout.DropOnWidget("tasks"));
    }

    [Fact]
    public void AGroupShowsItsHeader_AndItsMinimumSizeIsTheLargestOfItsMembers()
    {
        var layout = Editing();
        Merge(layout, "schedule", "notes");

        var slot = Slot(layout, "notes");

        Assert.True(slot.ShowHeader);
        Assert.Equal(slot.Members.Max(m => m.MinHeight), slot.MinHeight);
        Assert.Equal(slot.Members.Max(m => m.MinWidth), slot.MinWidth);
    }

    [Fact]
    public void TheHeaderToggle_DoesNothingForAGroupMember_AndTheChoiceComesBackOnUnmerge()
    {
        var layout = Editing();
        layout.ToggleHeader("notes");
        Assert.False(Slot(layout, "notes").ShowHeader);

        Merge(layout, "notes", "tasks");
        layout.ToggleHeader("notes");
        layout.ToggleHeader("tasks");
        Assert.True(Slot(layout, "tasks").ShowHeader);

        layout.Unmerge("notes");

        Assert.False(Slot(layout, "notes").ShowHeader);
        Assert.True(Slot(layout, "tasks").ShowHeader);
        Assert.False(Slot(layout, "tasks").IsGroup);
    }

    [Fact]
    public void ASchedule_WithItsHeaderOff_GetsItsOwnChoiceBack_AfterLeavingAGroup()
    {
        var layout = Editing();
        Merge(layout, "schedule", "notes");
        Assert.True(Slot(layout, "schedule").ShowHeader);

        layout.Unmerge("schedule");

        Assert.False(Slot(layout, "schedule").ShowHeader);
    }

    [Fact]
    public void SelectTab_ChangesTheActiveWidget_AndIsSavedAtOnceOutsideEditMode()
    {
        var layout = Editing();
        Merge(layout, "notes", "tasks");
        layout.DoneCommand.Execute(null);
        var saves = _repository.Saves;

        layout.SelectTab("notes");

        Assert.Equal("notes", Slot(layout, "tasks").ActiveWidget.Id);
        Assert.Equal(saves + 1, _repository.Saves);
        Assert.Equal("notes", _repository.Saved!.Columns.SelectMany(c => c.Widgets).Single(w => w.IsGroup).ActiveId);
    }

    [Fact]
    public void SelectTab_ToTheActiveTab_SavesNothing()
    {
        var layout = Editing();
        Merge(layout, "notes", "tasks");
        layout.DoneCommand.Execute(null);
        var saves = _repository.Saves;

        layout.SelectTab("tasks");

        Assert.Equal(saves, _repository.Saves);
    }

    [Fact]
    public void SelectTab_InEditMode_ChangesOnlyTheWorkingCopy_AndCancelPutsItBack()
    {
        var layout = Editing();
        Merge(layout, "notes", "tasks");
        layout.DoneCommand.Execute(null);
        layout.EnterEditCommand.Execute(null);
        var saves = _repository.Saves;

        layout.SelectTab("notes");
        Assert.Equal("notes", Slot(layout, "tasks").ActiveWidget.Id);
        Assert.Equal(saves, _repository.Saves);

        layout.CancelCommand.Execute(null);

        Assert.Equal("tasks", Slot(layout, "tasks").ActiveWidget.Id);
    }

    [Fact]
    public void Unmerge_InEditMode_PutsTheWidgetBelowTheGroup_AndDissolvesAGroupOfTwo()
    {
        var layout = Editing();
        Merge(layout, "notes", "tasks");

        layout.Unmerge("notes");

        Assert.All(layout.Columns.SelectMany(c => c.Widgets), w => Assert.False(w.IsGroup));
        Assert.Equal("tasks,notes,links", string.Join(",", layout.Columns[1].Widgets.Select(w => w.Id)));
    }

    [Fact]
    public void Unmerge_OutsideEditMode_DoesNothing()
    {
        var layout = Editing();
        Merge(layout, "notes", "tasks");
        layout.DoneCommand.Execute(null);

        layout.Unmerge("notes");

        Assert.True(Slot(layout, "tasks").IsGroup);
    }

    [Fact]
    public void ReorderTab_MovesATab_OnlyInEditMode()
    {
        var layout = Editing();
        Merge(layout, "notes", "tasks");
        layout.ReorderTab("tasks", 1);
        Assert.Equal(new[] { "notes", "tasks" }, Slot(layout, "tasks").Members.Select(m => m.Id));
        Assert.Equal("tasks", Slot(layout, "tasks").ActiveWidget.Id);

        layout.DoneCommand.Execute(null);
        layout.ReorderTab("tasks", 0);

        Assert.Equal(new[] { "notes", "tasks" }, Slot(layout, "tasks").Members.Select(m => m.Id));
    }

    [Fact]
    public void Done_SavesGroups_AndALayoutWithGroupsReloads()
    {
        var layout = Editing();
        Merge(layout, "notes", "tasks");
        layout.SelectTab("notes");
        layout.DoneCommand.Execute(null);

        var reloaded = new LayoutViewModel(_repository);

        var slot = Slot(reloaded, "notes");
        Assert.Equal(new[] { "tasks", "notes" }, slot.Members.Select(m => m.Id));
        Assert.Equal("notes", slot.ActiveWidget.Id);
    }

    [Fact]
    public void Cancel_UndoesAMerge_AndReset_BreaksEveryGroupUp()
    {
        var layout = Editing();
        Merge(layout, "notes", "tasks");
        layout.CancelCommand.Execute(null);
        Assert.All(layout.Columns.SelectMany(c => c.Widgets), w => Assert.False(w.IsGroup));

        layout.EnterEditCommand.Execute(null);
        Merge(layout, "notes", "tasks");
        layout.ResetCommand.Execute(null);

        Assert.All(layout.Columns.SelectMany(c => c.Widgets), w => Assert.False(w.IsGroup));
    }

    [Fact]
    public void Removing_AGroup_HidesAllItsWidgets_AndTheyAppearInTheTray()
    {
        var layout = Editing();
        Merge(layout, "notes", "tasks");

        layout.RemoveWidget("notes");

        Assert.Contains(layout.TrayWidgets, w => w.Id == "tasks");
        Assert.Contains(layout.TrayWidgets, w => w.Id == "notes");
        Assert.True(layout.IsTrayOpen);
    }

    [Fact]
    public void AMemberOfAGroup_CannotBeAddedAgainFromTheTray()
    {
        var layout = Editing();
        Merge(layout, "notes", "tasks");

        layout.AddWidgetFromTray("notes");

        Assert.Single(layout.Layout.WidgetIds, id => id == "notes");
        Assert.DoesNotContain(layout.TrayWidgets, w => w.Id == "notes");
    }

    [Fact]
    public void ADraggedGroup_CanBeMergedIntoAnotherCard_WhenItFits()
    {
        var layout = Editing();
        Merge(layout, "notes", "tasks");
        layout.BeginWidgetDrag("tasks");

        Assert.True(layout.CanMergeInto("links"));
        layout.DropOnWidget("links");

        Assert.Equal(3, Slot(layout, "links").Members.Count);
    }
}
