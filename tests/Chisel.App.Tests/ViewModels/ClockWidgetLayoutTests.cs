using Chisel.App.ViewModels;
using Chisel.Core.Layout;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class ClockWidgetLayoutTests
{
    [Fact]
    public void ClockIsRegisteredButHiddenInTheDefaultLayout()
    {
        Assert.NotNull(WidgetRegistry.Get(WidgetRegistry.Clock));
        Assert.DoesNotContain(WidgetRegistry.Clock, WidgetRegistry.DefaultLayout.WidgetIds);
        Assert.Contains(WidgetRegistry.Clock, WidgetRegistry.DefaultLayout.HiddenWidgetIds);
    }

    [Fact]
    public void AnExistingSavedLayout_LoadsWithoutTheClock()
    {
        var saved = new WidgetLayout(new[]
        {
            new LayoutColumn(1.3, new[] { new WidgetPlacement("schedule", 1) }),
            new LayoutColumn(1, new[] { new WidgetPlacement("scripture", 1), new WidgetPlacement("tasks", 2) }),
        }, new[] { "notes" });

        var layout = new LayoutViewModel(new Repo(saved)).Layout;

        Assert.Equal(new[] { "schedule", "scripture", "tasks" }, layout.WidgetIds);
        Assert.Equal(new[] { "notes", "clock", "weather", "media", "calculator", "calendar", "sysmonitor", "countdown", "focus", "habits", "converter" }, layout.HiddenWidgetIds);
        Assert.Equal(saved.Columns.Select(c => c.WidthWeight), layout.Columns.Select(c => c.WidthWeight));
    }

    [Fact]
    public void AddingAndRemovingTheClock_LeavesTheOtherWidgetsAlone()
    {
        var viewModel = new LayoutViewModel();
        var before = viewModel.Layout.Describe();
        viewModel.EnterEditCommand.Execute(null);

        viewModel.AddWidgetFromTray(WidgetRegistry.Clock);
        var added = viewModel.Layout;
        viewModel.RemoveWidget(WidgetRegistry.Clock);

        Assert.Equal("clock", added.Columns[0].Widgets[^1].WidgetId);
        Assert.Equal(1, added.WidgetIds.Count(id => id == "clock"));
        Assert.Equal(before.Split(" (hidden")[0], viewModel.Layout.Describe().Split(" (hidden")[0]);
    }

    private sealed class Repo(WidgetLayout saved) : IWidgetLayoutRepository
    {
        public WidgetLayout? Get() => saved;
        public void Save(WidgetLayout layout) { }
    }
}
