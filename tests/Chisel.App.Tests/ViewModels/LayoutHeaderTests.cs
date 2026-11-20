using Chisel.App.ViewModels;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.ViewModels;

// Header visibility is part of the layout's working copy: Done saves it, Cancel restores, Reset clears every choice.
public class LayoutHeaderTests
{
    private sealed class FakeLayoutRepository : IWidgetLayoutRepository
    {
        public WidgetLayout? Saved { get; set; }
        public WidgetLayout? Get() => Saved;
        public void Save(WidgetLayout layout) => Saved = layout;
    }

    private readonly FakeLayoutRepository _repository = new() { Saved = WidgetRegistry.DefaultLayout };

    private static bool Shown(LayoutViewModel layout, string id) =>
        layout.Columns.SelectMany(c => c.Widgets).Single(w => w.Id == id).ShowHeader;

    [Fact]
    public void RegistryDefaults_HideClockScheduleWeatherAndLinks_ShowTasksNotesAndCalculator()
    {
        var shown = WidgetRegistry.All.ToDictionary(w => w.Id, w => w.DefaultShowHeader);

        Assert.Equal(
            new Dictionary<string, bool>
            {
                [WidgetRegistry.Clock] = false, [WidgetRegistry.Schedule] = false, [WidgetRegistry.Weather] = false, [WidgetRegistry.Media] = false, [WidgetRegistry.PictureFrame] = false, [WidgetRegistry.Calculator] = true,
                [WidgetRegistry.Links] = false, [WidgetRegistry.Tasks] = true, [WidgetRegistry.Notes] = true,
            },
            shown);
    }

    [Fact]
    public void Toggle_FlipsTheEffectiveVisibility_OnlyInEditMode()
    {
        var layout = new LayoutViewModel(_repository);

        layout.ToggleHeader("schedule");
        Assert.False(Shown(layout, "schedule"));

        layout.EnterEditCommand.Execute(null);
        layout.ToggleHeader("schedule");
        layout.ToggleHeader("tasks");

        Assert.True(Shown(layout, "schedule"));
        Assert.False(Shown(layout, "tasks"));
        Assert.True(Shown(layout, "notes"));
    }

    [Fact]
    public void Done_SavesTheChoice()
    {
        var layout = new LayoutViewModel(_repository);
        layout.EnterEditCommand.Execute(null);
        layout.ToggleHeader("schedule");

        layout.DoneCommand.Execute(null);

        Assert.True(_repository.Saved!.Find("schedule") is { } at
            && _repository.Saved.Columns[at.Column].Widgets[at.Index].ShowHeader == true);
        Assert.True(Shown(new LayoutViewModel(_repository), "schedule"));
    }

    [Fact]
    public void Cancel_RestoresTheSavedVisibility()
    {
        var layout = new LayoutViewModel(_repository);
        layout.EnterEditCommand.Execute(null);
        layout.ToggleHeader("schedule");
        layout.ToggleHeader("tasks");

        layout.CancelCommand.Execute(null);

        Assert.False(Shown(layout, "schedule"));
        Assert.True(Shown(layout, "tasks"));
    }

    [Fact]
    public void Reset_RestoresTheDefaultVisibilityForEveryWidget()
    {
        var layout = new LayoutViewModel(_repository);
        layout.EnterEditCommand.Execute(null);
        layout.ToggleHeader("schedule");
        layout.ToggleHeader("tasks");

        layout.ResetCommand.Execute(null);

        Assert.False(Shown(layout, "schedule"));
        Assert.True(Shown(layout, "tasks"));
    }
}
