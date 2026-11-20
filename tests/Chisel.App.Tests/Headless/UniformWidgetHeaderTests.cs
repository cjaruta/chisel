using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Weather;

namespace Chisel.App.Tests.Headless;

// Every widget card has the same header: one height, background, padding and bottom edge. Hidden, the whole row is gone.
public class UniformWidgetHeaderTests
{
    private static readonly string[] Ids = ["tasks", "notes", "clock", "schedule", "weather", "links", "calculator"];

    private static List<WidgetFrame> Frames(Window window) =>
        window.GetVisualDescendants().OfType<LayoutHost>().Single().Frames.SelectMany(c => c).ToList();

    private static WidgetFrame Frame(Window window, string id) => Frames(window).Single(f => f.WidgetId == id);

    private static bool HasHeaderParts(WidgetFrame frame) =>
        frame.GetVisualDescendants().Any(v => v is Control c && (c.Classes.Contains("widgetHeader") || c.Classes.Contains("widgetHeading")));

    // Every widget on the dashboard, weather ready, each header shown or hidden as asked.
    private static MainWindow Show(bool headers)
    {
        var store = new Chisel.App.Services.MemoryWeatherStore();
        store.SaveLocation(new WeatherLocation("Testville", 1, 2));
        var window = UiSnapshot.ShowMainWindow(weatherStore: store);
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout;
        layout.EnterEditCommand.Execute(null);
        layout.AddWidgetFromTray(WidgetRegistry.Clock);
        layout.AddWidgetFromTray(WidgetRegistry.Weather);
        layout.AddWidgetFromTray(WidgetRegistry.Calculator);
        main.Weather.ShowCached();
        main.Weather.Show(WeatherState.Ready, new WeatherSnapshot(20, 18, WeatherCondition.Rain, 24, 12, []));
        Dispatcher.UIThread.RunJobs();
        foreach (var id in Ids.Where(id => Frame(window, id).ShowHeader != headers))
        {
            layout.ToggleHeader(id);
        }
        layout.DoneCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void EveryHeader_HasTheSameHeightBackgroundAndPadding()
    {
        var window = Show(headers: true);
        var headers = Ids.Select(id => Frame(window, id).Header!).ToList();

        Assert.All(headers, h => Assert.True(h.IsVisible && h.Bounds.Height > 0));
        Assert.Single(headers.Select(h => h.Bounds.Height).Distinct());
        Assert.Single(headers.Select(h => ((ISolidColorBrush)h.Background!).Color).Distinct());
        Assert.Single(headers.Select(h => h.Padding).Distinct());
        Assert.Single(headers.Select(h => h.BorderThickness).Distinct());
        window.Close();
    }

    [AvaloniaFact]
    public void HidingRemovesTheWholeHeader_AndTheBodyTakesTheSpace()
    {
        var window = Show(headers: false);
        foreach (var id in Ids)
        {
            var frame = Frame(window, id);
            Assert.False(HasHeaderParts(frame), id);
            Assert.Null(frame.Header.GetVisualParent());
            Assert.True(frame.Content.Children[0].Bounds.Top < 1, id);
        }
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void WeatherBackdrop_IsOutsideTheHeader(bool header)
    {
        var window = Show(header);
        var frame = Frame(window, "weather");
        var view = frame.View;
        Assert.IsType<LinearGradientBrush>(view.Background);

        var top = view.TranslatePoint(new Avalonia.Point(0, 0), frame)!.Value.Y;
        if (header)
        {
            Assert.IsNotType<LinearGradientBrush>(frame.Header!.Background);
            Assert.True(top >= frame.Header.Bounds.Bottom - 0.5);
        }
        else
        {
            Assert.True(top < 2); // the card's own 1px border
            Assert.True(view.Bounds.Height >= frame.Content.Bounds.Height - 0.5, $"{view.Bounds} vs {frame.Content.Bounds}");
        }
        window.Close();
    }

    [AvaloniaFact]
    public void WeatherLocation_IsInTheBodyWhetherOrNotTheHeaderIsShown()
    {
        foreach (var header in new[] { true, false })
        {
            var window = Show(header);
            Assert.Equal("Testville", Frame(window, "weather").View.GetVisualDescendants().OfType<TextBlock>()
                .Single(t => t.Name == "WeatherLocation").Text);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScheduleAndTaskButtons_StayInTheBody_WhenTheHeaderIsHidden()
    {
        var window = Show(headers: false);
        var buttons = Frame(window, "schedule").GetVisualDescendants().OfType<Button>();
        Assert.Contains(buttons, b => b.Name == "NowButton" && b.IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(true, "uniform-headers-shown")]
    [InlineData(false, "uniform-headers-hidden")]
    public void Render_EveryWidget(bool headers, string name)
    {
        var window = Show(headers);
        UiSnapshot.Render(window, name);
        window.Close();
    }
}
