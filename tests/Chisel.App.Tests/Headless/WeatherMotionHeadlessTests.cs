using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.App.Views.Widgets.Backdrop;
using Chisel.Core.Weather;

namespace Chisel.App.Tests.Headless;

public class WeatherMotionHeadlessTests
{
    private static readonly WeatherSnapshot Rain = new(14, 12, WeatherCondition.Rain, 16, 9,
        Enumerable.Range(0, 8).Select(i => new HourlyWeather(new System.TimeOnly(10 + i, 0), WeatherCondition.Rain, 14 - i)).ToList());

    private static (Chisel.App.Views.MainWindow Window, MainViewModel Main, WeatherMotionLayer Layer) Open(WeatherState state)
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.Weather.Show(state, state == WeatherState.Ready ? Rain : null);
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Weather, 0, layout.Columns[0].Widgets.Count, 0.6));
        Dispatcher.UIThread.RunJobs();
        var view = window.GetVisualDescendants().OfType<WeatherWidgetView>().Single();
        return (window, main, WeatherMotionLayer.Of(view)!);
    }

    [AvaloniaFact]
    public void Motion_RunsWhenReady_NotWhileLoading_AndNotInEditMode()
    {
        var (window, main, layer) = Open(WeatherState.Ready);
        Assert.True(layer.IsAnimating);
        Assert.Equal(MotionKind.Rain, layer.Kind);
        UiSnapshot.Render(window, "weather-motion-rain");

        main.CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(layer.IsAnimating);

        main.Layout.CancelCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(layer.IsAnimating);

        main.Weather.Show(WeatherState.Loading, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(layer.IsAnimating);
    }

    [AvaloniaFact]
    public void Motion_StopsSchedulingFrames_WhenTheWidgetIsRemoved()
    {
        var (_, main, layer) = Open(WeatherState.Ready);
        Assert.True(layer.IsAnimating);

        main.Layout.Apply(main.Layout.Layout.RemoveWidget(WidgetRegistry.Weather));
        Dispatcher.UIThread.RunJobs();

        Assert.False(layer.IsAnimating);
    }

    [AvaloniaFact]
    public void Motion_ShowsNothingWhenLoading()
    {
        var (_, _, layer) = Open(WeatherState.Loading);
        Assert.False(layer.IsAnimating);
    }
}
