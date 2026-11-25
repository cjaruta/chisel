using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.ViewModels;

// The widget registry, the default layout, and the view model the layout host is built from.
public class LayoutViewModelTests
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

    private static WidgetLayout Layout(params LayoutColumn[] columns) => new(columns);

    private static LayoutColumn Col(double width, params (string Id, double Height)[] widgets) =>
        new(width, widgets.Select(w => new WidgetPlacement(w.Id, w.Height)).ToList());

    private static string Describe(LayoutViewModel layout) =>
        string.Join(" | ", layout.Columns.Select(c => $"{c.WidthWeight}[{string.Join(", ", c.Widgets.Select(w => $"{w.Id}:{w.HeightWeight}"))}]"));

    [Fact]
    public void Registry_HasTheWidgetsWithStableIdsTitlesAndViews()
    {
        Assert.Equal(new[] { "schedule", "tasks", "notes", "clock", "weather", "media", "pictureframe", "calculator", "scripture", "calendar", "sysmonitor", "countdown", "focus", "habits", "converter", "clipboard" }, WidgetRegistry.All.Select(w => w.Id));
        Assert.Equal(new[] { "Today", "Tasks", "Notes", "Clock", "Weather", "Media", "Picture frame", "Calculator", "Daily verse", "Calendar", "System monitor", "Countdown", "Focus timer", "Habits", "Converter", "Clipboard history" }, WidgetRegistry.All.Select(w => w.Title));
        Assert.All(WidgetRegistry.All, w =>
        {
            Assert.True(w.MinHeight > 0);
            Assert.True(typeof(WidgetView).IsAssignableFrom(w.ViewType), w.Id);
        });
        Assert.Equal("tasks", WidgetRegistry.Get("tasks")!.Id);
        Assert.Null(WidgetRegistry.Get("forecast"));
    }

    [Fact]
    public void Registry_OnlyTheScheduleTheTasksAndTheNotepadScrollThemselves()
    {
        Assert.Equal(new[] { "schedule", "tasks", "notes", "clock", "weather", "media", "pictureframe", "scripture", "sysmonitor", "countdown", "focus", "habits", "converter", "clipboard" }, WidgetRegistry.All.Where(w => !w.ScrollsContent).Select(w => w.Id));
    }

    [Fact]
    public void DefaultLayout_IsScheduleOnTheLeftAndTheListsStackedOnTheRight()
    {
        Assert.Equal("1.3[schedule:1] | 1[tasks:1.05, notes:1.1] (hidden: clock, weather, media, calculator, scripture, calendar, sysmonitor, countdown, focus, habits, converter, clipboard)", WidgetRegistry.DefaultLayout.Describe());
    }

    [Fact]
    public void WithNoRepository_UsesTheDefaultLayout()
    {
        var layout = new LayoutViewModel();

        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Layout.Describe());
    }

    [Fact]
    public void WithNothingSaved_UsesTheDefaultAndSavesIt()
    {
        var repository = new FakeLayoutRepository();

        var layout = new LayoutViewModel(repository);

        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), layout.Layout.Describe());
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(WidgetRegistry.DefaultLayout.Describe(), repository.Saved!.Describe());
    }

    [Fact]
    public void WithASavedLayout_LoadsItNormalized_AndSavesOnceToPlaceANewWidget()
    {
        // Notes is neither placed nor hidden here, so it is new: it joins the bottom of the first column.
        var repository = new FakeLayoutRepository
        {
            Saved = Layout(Col(1, ("tasks", 2), ("forecast", 1)), Col(0), Col(2, ("schedule", 1), ("scripture", 0.5))),
        };

        var layout = new LayoutViewModel(repository);

        Assert.Equal("1[tasks:1.29, notes:0.71] | 2[schedule:1, scripture:0.5] (hidden: clock, weather, media, calculator, calendar, sysmonitor, countdown, focus, habits, converter, clipboard)", layout.Layout.Describe());
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(layout.Layout.Describe(), repository.Saved!.Describe());

        new LayoutViewModel(repository);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public void WithASavedLayout_ThatHidesAWidget_KeepsItHiddenWithoutSaving()
    {
        var repository = new FakeLayoutRepository
        {
            Saved = new WidgetLayout(new[] { Col(1.3, ("schedule", 1)), Col(1, ("tasks", 1), ("scripture", 1)) }, new[] { "notes" }),
        };

        var layout = new LayoutViewModel(repository);

        Assert.Equal("1.3[schedule:1] | 1[tasks:1, scripture:1] (hidden: notes, clock, weather, media, calculator, calendar, sysmonitor, countdown, focus, habits, converter, clipboard)", layout.Layout.Describe());
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public void Columns_AreBuiltFromTheLayout()
    {
        var layout = new LayoutViewModel();

        Assert.Equal("1.3[schedule:1] | 1[tasks:1.05, notes:1.1]", Describe(layout));
        var tasks = layout.Columns[1].Widgets[0];
        Assert.Equal(("Tasks", WidgetRegistry.Get("tasks")!.MinHeight), (tasks.Title, tasks.MinHeight));
        Assert.Same(WidgetRegistry.Get("tasks"), tasks.Widget);
    }

    [Fact]
    public void Apply_ReplacesTheLayoutAndRebuildsTheColumns()
    {
        var layout = new LayoutViewModel();
        var changed = new List<string?>();
        layout.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        layout.Apply(Layout(Col(1, ("notes", 1), ("scripture", 1)), Col(2, ("schedule", 1)), Col(1, ("tasks", 3))));

        Assert.Equal("1[notes:1, scripture:1] | 2[schedule:1] | 1[tasks:3]", Describe(layout));
        Assert.Contains(nameof(LayoutViewModel.Layout), changed);
        Assert.Contains(nameof(LayoutViewModel.Columns), changed);
    }

    [Fact]
    public void Apply_NormalizesTheNewLayout()
    {
        var layout = new LayoutViewModel();

        layout.Apply(Layout(Col(1, ("schedule", 0), ("forecast", 1))));

        // Forecast is dropped, schedule raised to the minimum, and the widgets never seen join the first column.
        Assert.Equal("1[schedule:0.133, tasks:1.4, notes:1.467] (hidden: clock, weather, media, calculator, scripture, calendar, sysmonitor, countdown, focus, habits, converter, clipboard)", layout.Layout.Describe());
    }

    // Quick links used to be a dashboard widget; it now lives in the top bar. A saved layout that still has it must load
    // without it and without a gap.

    [Fact]
    public void QuickLinks_IsNoLongerAWidget_SoTheLibraryCannotOfferIt()
    {
        Assert.DoesNotContain(WidgetRegistry.All, w => w.Id == "links");
        Assert.Null(WidgetRegistry.Get("links"));
        Assert.DoesNotContain("links", WidgetRegistry.DefaultLayout.WidgetIds);
        Assert.DoesNotContain("links", WidgetRegistry.DefaultLayout.HiddenWidgetIds);
    }

    [Fact]
    public void ASavedLayoutWithTheOldLinksCard_LoadsWithoutIt_AndTheNeighboursFillTheColumn()
    {
        var repository = new FakeLayoutRepository
        {
            Saved = new WidgetLayout(new[] { Col(1.3, ("schedule", 1)), Col(1, ("tasks", 1.05), ("notes", 1.1), ("links", 0.85)) },
                new[] { "clock", "weather", "media", "calculator", "scripture" }),
        };

        var layout = new LayoutViewModel(repository);

        Assert.Equal("1.3[schedule:1] | 1[tasks:1.05, notes:1.1] (hidden: clock, weather, media, calculator, scripture, calendar, sysmonitor, countdown, focus, habits, converter, clipboard)", layout.Layout.Describe());
        Assert.Equal(new[] { 1, 2 }, layout.Columns.Select(c => c.Widgets.Count).ToArray());
        Assert.DoesNotContain("links", layout.Layout.WidgetIds);
    }

    [Fact]
    public void ALinksCardAloneInItsColumn_TakesTheColumnWithIt_WithNoBlankGap()
    {
        var repository = new FakeLayoutRepository
        {
            Saved = new WidgetLayout(new[] { Col(1.3, ("schedule", 1)), Col(1, ("tasks", 1), ("notes", 1)), Col(0.7, ("links", 1)) },
                new[] { "clock", "weather", "media", "calculator", "scripture" }),
        };

        var layout = new LayoutViewModel(repository);

        Assert.Equal(2, layout.Columns.Count);
        Assert.Equal("1.3[schedule:1] | 1[tasks:1, notes:1] (hidden: clock, weather, media, calculator, scripture, calendar, sysmonitor, countdown, focus, habits, converter, clipboard)", layout.Layout.Describe());
    }

    [Fact]
    public void ALinksTabInsideAGroup_LeavesTheOtherWidgetAsAPlainCard()
    {
        var group = new WidgetPlacement("notes", 1.1, null, new[] { new GroupMember("notes", true), new GroupMember("links", false) }, 0, GroupViewMode.Tabs);
        var repository = new FakeLayoutRepository
        {
            Saved = new WidgetLayout(new[] { Col(1.3, ("schedule", 1)), new LayoutColumn(1, new[] { new WidgetPlacement("tasks", 1.05), group }) },
                new[] { "clock", "weather", "media", "calculator", "scripture" }),
        };

        var layout = new LayoutViewModel(repository);

        var notes = layout.Layout.Columns[1].Widgets[1];
        Assert.False(notes.IsGroup);
        Assert.Equal("notes", notes.WidgetId);
        Assert.DoesNotContain("links", layout.Layout.WidgetIds);
    }

    [Fact]
    public void ALinksIdInTheHiddenList_IsIgnored_AndNeverReturns()
    {
        var repository = new FakeLayoutRepository
        {
            Saved = new WidgetLayout(new[] { Col(1.3, ("schedule", 1)), Col(1, ("tasks", 1), ("notes", 1)) },
                new[] { "links", "clock", "weather", "media", "calculator", "scripture" }),
        };

        var layout = new LayoutViewModel(repository);

        Assert.DoesNotContain("links", layout.Layout.HiddenWidgetIds);
        Assert.DoesNotContain("links", layout.Layout.WidgetIds);
    }
}
