using Chisel.App.ViewModels;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.ViewModels;

// Split view in the layout view model: the toggle in normal and edit mode (saved at once, or part of the working
// copy), refusals, the pane widths, and Cancel / Reset.
public class LayoutSplitViewTests
{
    private sealed class FakeLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; set; }
        public int Saves { get; private set; }
        public WidgetLayout? Get() => Saved;
        public void Save(WidgetLayout layout) { Saved = layout; Saves++; }
    }

    private const double Wide = 900;

    private readonly FakeLayoutRepository _repository = new() { Saved = WidgetRegistry.DefaultLayout };

    // tasks + notes as a group (and links as a plain card), saved.
    private LayoutViewModel Grouped()
    {
        var layout = new LayoutViewModel(_repository);
        layout.EnterEditCommand.Execute(null);
        layout.BeginWidgetDrag("notes");
        layout.DropOnWidget("tasks");
        layout.DoneCommand.Execute(null);
        return layout;
    }

    private static WidgetSlot Slot(LayoutViewModel layout, string id) =>
        layout.Columns.SelectMany(c => c.Widgets).Single(w => w.Members.Any(m => m.Id == id));

    private static WidgetPlacement Saved(FakeLayoutRepository repository) =>
        repository.Saved!.Columns.SelectMany(c => c.Widgets).Single(w => w.IsGroup);

    [Fact]
    public void SetViewMode_OutsideEditMode_SwitchesAndSavesAtOnce()
    {
        var layout = Grouped();
        var saves = _repository.Saves;

        var refusal = layout.SetViewMode("tasks", GroupViewMode.Split, Wide);

        Assert.Null(refusal);
        Assert.Equal(GroupViewMode.Split, Slot(layout, "tasks").ViewMode);
        Assert.Equal(saves + 1, _repository.Saves);
        Assert.Equal(GroupViewMode.Split, Saved(_repository).ViewMode);
    }

    [Fact]
    public void SetViewMode_InEditMode_IsPartOfTheWorkingCopy_UntilDone()
    {
        var layout = Grouped();
        layout.EnterEditCommand.Execute(null);
        var saves = _repository.Saves;

        layout.SetViewMode("tasks", GroupViewMode.Split, Wide);

        Assert.Equal(GroupViewMode.Split, Slot(layout, "tasks").ViewMode);
        Assert.Equal(saves, _repository.Saves);
        layout.DoneCommand.Execute(null);
        Assert.Equal(GroupViewMode.Split, Saved(_repository).ViewMode);
    }

    [Fact]
    public void Cancel_PutsTheViewModeBack()
    {
        var layout = Grouped();
        layout.EnterEditCommand.Execute(null);
        layout.SetViewMode("tasks", GroupViewMode.Split, Wide);

        layout.CancelCommand.Execute(null);

        Assert.Equal(GroupViewMode.Tabs, Slot(layout, "tasks").ViewMode);
        Assert.Equal(GroupViewMode.Tabs, Saved(_repository).ViewMode);
    }

    [Fact]
    public void Reset_GoesBackToTheDefault_WhichHasNoGroups()
    {
        var layout = Grouped();
        layout.EnterEditCommand.Execute(null);
        layout.SetViewMode("tasks", GroupViewMode.Split, Wide);

        layout.ResetCommand.Execute(null);

        Assert.All(layout.Columns.SelectMany(c => c.Widgets), w => Assert.Equal(GroupViewMode.Tabs, w.ViewMode));
    }

    [Fact]
    public void SetViewMode_WhenTooNarrow_IsRefusedWithAReason_AndNothingIsSaved()
    {
        var layout = Grouped();
        var saves = _repository.Saves;

        var refusal = layout.SetViewMode("tasks", GroupViewMode.Split, 300);

        Assert.False(string.IsNullOrWhiteSpace(refusal));
        Assert.Equal(GroupViewMode.Tabs, Slot(layout, "tasks").ViewMode);
        Assert.Equal(saves, _repository.Saves);
        Assert.Equal(refusal, layout.SplitRefusal("tasks", 300));
        Assert.Null(layout.SplitRefusal("tasks", Wide));
    }

    [Fact]
    public void SetViewMode_AKeptActiveTab_ComesBackWithTabs()
    {
        var layout = Grouped();
        layout.SelectTab("notes");

        layout.SetViewMode("tasks", GroupViewMode.Split, Wide);
        layout.SetViewMode("tasks", GroupViewMode.Tabs, Wide);

        Assert.Equal("notes", Slot(layout, "tasks").ActiveWidget.Id);
    }

    [Fact]
    public void PaneResize_PreviewsLive_AndSavesOnCommitOutsideEditMode()
    {
        var layout = Grouped();
        layout.SetViewMode("tasks", GroupViewMode.Split, Wide);
        var saves = _repository.Saves;

        layout.BeginPaneResize();
        layout.PreviewPaneResize("tasks", 0.1, 0.2, 0.2);
        Assert.True(Slot(layout, "tasks").PaneWeights![0] > Slot(layout, "tasks").PaneWeights![1]);
        Assert.Equal(saves, _repository.Saves);
        layout.CommitPaneResize();

        Assert.Equal(saves + 1, _repository.Saves);
        Assert.True(Saved(_repository).Members![0].WidthWeight > Saved(_repository).Members![1].WidthWeight);
    }

    [Fact]
    public void PaneResize_Cancelled_PutsTheWidthsBack()
    {
        var layout = Grouped();
        layout.SetViewMode("tasks", GroupViewMode.Split, Wide);

        layout.BeginPaneResize();
        layout.PreviewPaneResize("tasks", 0.1, 0.2, 0.2);
        layout.CancelPaneResize();

        Assert.Equal(new[] { 1.0, 1.0 }, Slot(layout, "tasks").PaneWeights);
    }

    [Fact]
    public void Dropping_OnASplitGroup_AddsAPane_WhenItFits_AndOtherwiseGoesToTabs()
    {
        var layout = Grouped();
        layout.SetViewMode("tasks", GroupViewMode.Split, Wide);
        layout.EnterEditCommand.Execute(null);
        layout.BeginWidgetDrag("links");
        layout.DropOnWidget("tasks", Wide);
        Assert.Equal(GroupViewMode.Split, Slot(layout, "tasks").ViewMode);
        Assert.Equal(3, Slot(layout, "tasks").Members.Count);

        layout.CancelCommand.Execute(null);
        layout.EnterEditCommand.Execute(null);
        layout.BeginWidgetDrag("links");
        layout.DropOnWidget("tasks", 400);
        Assert.Equal(GroupViewMode.Tabs, Slot(layout, "tasks").ViewMode);
        Assert.Equal(3, Slot(layout, "tasks").Members.Count);
    }

    [Fact]
    public void MinWidths_AreSetPerWidget_AndTheClockIsNarrow()
    {
        Assert.InRange(WidgetRegistry.Get(WidgetRegistry.Clock)!.MinWidth, 180, 200);
        Assert.All(WidgetRegistry.All, w => Assert.InRange(w.MinWidth, 180, LayoutLimits.MinimumColumnWidth));
    }

    [Fact]
    public void ASplitSlot_NeedsThePanesMinimumsPlusGaps_ATabsSlotTheLargestMinimum()
    {
        var layout = Grouped();
        var tabs = Slot(layout, "tasks");
        var expectedSplit = tabs.Members.Sum(m => m.MinWidth) + GroupSplit.Gap * (tabs.Members.Count - 1);

        layout.SetViewMode("tasks", GroupViewMode.Split, Wide);

        Assert.Equal(tabs.Members.Max(m => m.MinWidth), tabs.MinWidth);
        Assert.Equal(expectedSplit, Slot(layout, "tasks").SplitMinWidth);
    }
}
