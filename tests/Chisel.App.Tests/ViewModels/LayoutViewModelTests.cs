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
        Assert.Equal(new[] { "schedule", "tasks", "notes", "links", "clock", "weather", "media" }, WidgetRegistry.All.Select(w => w.Id));
        Assert.Equal(new[] { "Today", "Tasks", "Notes", "Quick links", "Clock", "Weather", "Media" }, WidgetRegistry.All.Select(w => w.Title));
        Assert.All(WidgetRegistry.All, w =>
        {
            Assert.True(w.MinHeight > 0);
            Assert.True(typeof(WidgetView).IsAssignableFrom(w.ViewType), w.Id);
        });
        Assert.Equal("tasks", WidgetRegistry.Get("tasks")!.Id);
        Assert.Null(WidgetRegistry.Get("forecast"));
    }

    [Fact]
    public void Registry_OnlyTheScheduleScrollsItself()
    {
        Assert.Equal(new[] { "schedule", "clock", "weather", "media" }, WidgetRegistry.All.Where(w => !w.ScrollsContent).Select(w => w.Id));
    }

    [Fact]
    public void DefaultLayout_IsScheduleOnTheLeftAndTheListsStackedOnTheRight()
    {
        Assert.Equal("1.3[schedule:1] | 1[tasks:1.05, notes:1.1, links:0.85] (hidden: clock, weather, media)", WidgetRegistry.DefaultLayout.Describe());
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
            Saved = Layout(Col(1, ("tasks", 2), ("forecast", 1)), Col(0), Col(2, ("schedule", 1), ("links", 0.5))),
        };

        var layout = new LayoutViewModel(repository);

        Assert.Equal("1[tasks:1.29, notes:0.71] | 2[schedule:1, links:0.5] (hidden: clock, weather, media)", layout.Layout.Describe());
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
            Saved = new WidgetLayout(new[] { Col(1.3, ("schedule", 1)), Col(1, ("tasks", 1), ("links", 1)) }, new[] { "notes" }),
        };

        var layout = new LayoutViewModel(repository);

        Assert.Equal("1.3[schedule:1] | 1[tasks:1, links:1] (hidden: notes, clock, weather, media)", layout.Layout.Describe());
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public void Columns_AreBuiltFromTheLayout()
    {
        var layout = new LayoutViewModel();

        Assert.Equal("1.3[schedule:1] | 1[tasks:1.05, notes:1.1, links:0.85]", Describe(layout));
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

        layout.Apply(Layout(Col(1, ("notes", 1), ("links", 1)), Col(2, ("schedule", 1)), Col(1, ("tasks", 3))));

        Assert.Equal("1[notes:1, links:1] | 2[schedule:1] | 1[tasks:3]", Describe(layout));
        Assert.Contains(nameof(LayoutViewModel.Layout), changed);
        Assert.Contains(nameof(LayoutViewModel.Columns), changed);
    }

    [Fact]
    public void Apply_NormalizesTheNewLayout()
    {
        var layout = new LayoutViewModel();

        layout.Apply(Layout(Col(1, ("schedule", 0), ("forecast", 1))));

        // Forecast is dropped, schedule raised to the minimum, and the widgets never seen join the first column.
        Assert.Equal("1[schedule:0.129, tasks:1.355, notes:1.419, links:1.097] (hidden: clock, weather, media)", layout.Layout.Describe());
    }
}
