using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Tests.Headless;

// Widget titles are hidden by default for the clock, schedule, weather and quick links, and a small badge
// in edit mode shows or hides each one.
public class WidgetHeaderHeadlessTests
{
    private static List<WidgetFrame> Frames(Window window) =>
        window.GetVisualDescendants().OfType<LayoutHost>().Single().Frames.SelectMany(c => c).ToList();

    private static WidgetFrame Frame(Window window, string id) => Frames(window).Single(f => f.WidgetId == id);

    private static bool TitleShown(WidgetFrame frame) =>
        frame.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("widgetHeading")).IsEffectivelyVisible;

    private static LayoutViewModel Layout(Window window) => ((MainViewModel)window.DataContext!).Layout;

    private static MainWindow ShowWithEveryWidget()
    {
        var window = UiSnapshot.ShowMainWindow();
        var layout = Layout(window);
        layout.EnterEditCommand.Execute(null);
        layout.AddWidgetFromTray(WidgetRegistry.Clock);
        layout.AddWidgetFromTray(WidgetRegistry.Weather);
        layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void ClockScheduleWeatherAndLinks_ShowNoTitleByDefault_TasksAndNotesDo()
    {
        var window = ShowWithEveryWidget();

        Assert.All(new[] { "clock", "schedule", "weather", "links" }, id => Assert.False(TitleShown(Frame(window, id)), id));
        Assert.All(new[] { "tasks", "notes" }, id => Assert.True(TitleShown(Frame(window, id)), id));
        window.Close();
    }

    [AvaloniaFact]
    public void TheBadgeShowsAndHidesTheTitle_AndDoesNotMoveTheCard()
    {
        var window = UiSnapshot.ShowMainWindow();
        var layout = Layout(window);
        layout.EnterEditCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var notes = Frame(window, "notes");
        var before = notes.Bounds;

        notes.HeaderButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.False(TitleShown(notes));
        Assert.Equal(before, notes.Bounds);
        Assert.Equal("Show title", ToolTip.GetTip(notes.HeaderButton));

        notes.HeaderButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.True(TitleShown(notes));
        Assert.Equal("Hide title", ToolTip.GetTip(notes.HeaderButton));
        window.Close();
    }

    [AvaloniaFact]
    public void InEditMode_TheWidgetNameIsATooltipOnTheCard()
    {
        var window = UiSnapshot.ShowMainWindow();
        Layout(window).EnterEditCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Quick links", ToolTip.GetTip(Frame(window, "links").EditBar));
        window.Close();
    }

    [AvaloniaFact]
    public void CancelPutsTheTitleBack()
    {
        var window = UiSnapshot.ShowMainWindow();
        var layout = Layout(window);
        layout.EnterEditCommand.Execute(null);
        layout.ToggleHeader("notes");
        Dispatcher.UIThread.RunJobs();
        Assert.False(TitleShown(Frame(window, "notes")));

        layout.CancelCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(TitleShown(Frame(window, "notes")));
        window.Close();
    }

    [AvaloniaFact]
    public void WeatherWithoutATitle_ShowsThePlaceNameAtTheTopLeft_AndRendersBothModes()
    {
        var store = new Chisel.App.Services.MemoryWeatherStore();
        store.SaveLocation(new Chisel.Core.Weather.WeatherLocation("Testville", 1, 2));
        var window = UiSnapshot.ShowMainWindow(weatherStore: store);
        var layout = Layout(window);
        layout.EnterEditCommand.Execute(null);
        layout.AddWidgetFromTray(WidgetRegistry.Clock);
        layout.AddWidgetFromTray(WidgetRegistry.Weather);
        layout.DoneCommand.Execute(null);
        var main = (MainViewModel)window.DataContext!;
        main.Weather.ShowCached();
        main.Weather.Show(WeatherState.Ready, new Chisel.Core.Weather.WeatherSnapshot(
            20, 18, Chisel.Core.Weather.WeatherCondition.Rain, 24, 12, []));
        Dispatcher.UIThread.RunJobs();

        var weather = Frame(window, "weather");
        var place = weather.GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(t => t.Name == "WeatherLocation");
        Assert.Equal("Testville", place?.Text);
        UiSnapshot.Render(window, "widget-headers-normal-1200x760");

        layout.EnterEditCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        UiSnapshot.Render(window, "widget-headers-edit-1200x760");
        window.Close();
    }

    // The title over the weather backdrop uses the primary text color that the contrast test checks on every gradient stop.
    [AvaloniaFact]
    public void WeatherTitle_WhenShown_UsesThePrimaryTextColor()
    {
        var window = ShowWithEveryWidget();
        var layout = Layout(window);
        layout.EnterEditCommand.Execute(null);
        layout.ToggleHeader("weather");
        ((MainViewModel)window.DataContext!).Weather.Show(WeatherState.Ready, new Chisel.Core.Weather.WeatherSnapshot(
            20, 18, Chisel.Core.Weather.WeatherCondition.Clear, 24, 12, []));
        Dispatcher.UIThread.RunJobs();

        var heading = Frame(window, "weather").GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("widgetHeading"));
        Assert.True(heading.IsEffectivelyVisible);
        Assert.Equal(0xFF000000 | Chisel.Core.Weather.WeatherBackdrop.PrimaryText, ((Avalonia.Media.ISolidColorBrush)heading.Foreground!).Color.ToUInt32());
        window.Close();
    }
}
