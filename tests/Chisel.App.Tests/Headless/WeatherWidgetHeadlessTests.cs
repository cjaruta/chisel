using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.Core.Weather;

namespace Chisel.App.Tests.Headless;

public class WeatherWidgetHeadlessTests
{
    private static readonly WeatherSnapshot Sample = new(20.4, 18, WeatherCondition.PartlyCloudy, 24, 12,
        Enumerable.Range(0, 8).Select(i => new HourlyWeather(new TimeOnly(10 + i, 0), WeatherCondition.Cloudy, 20 - i)).ToList());

    // Opens the window with only the weather widget added; weight is its share of the first column's height.
    private static (Chisel.App.Views.MainWindow Window, MainViewModel Main) WindowWithWeather(double weight, WeatherState state = WeatherState.Ready)
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.Weather.Show(state, state == WeatherState.Ready ? Sample : null);
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Weather, 0, layout.Columns[0].Widgets.Count, weight));
        Dispatcher.UIThread.RunJobs();
        return (window, main);
    }

    private static T? Named<T>(Chisel.App.Views.MainWindow w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);

    private static double ContentHeight(Chisel.App.Views.MainWindow w)
    {
        var view = w.GetVisualDescendants().OfType<WeatherWidgetView>().Single();
        return view.Bounds.Height - view.Padding.Bottom;
    }

    [AvaloniaFact]
    public void Weather_IsInTheTray_CanBeAdded_AndDoesNotOpenTheTrayByItself()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(main.Layout.TrayWidgets, w => w.Id == WidgetRegistry.Weather);
        Assert.Empty(window.GetVisualDescendants().OfType<WeatherWidgetView>());

        main.Layout.AddWidgetFromTray(WidgetRegistry.Weather);
        Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().OfType<WeatherWidgetView>().Single();
        Assert.False(view.IsHitTestVisible);
    }

    [AvaloniaFact]
    public void Small_ShowsTemperatureIconAndCondition_Only()
    {
        var (window, _) = WindowWithWeather(0.2);
        Assert.Equal(WeatherSizeTier.Small, WeatherLayout.SizeTier(0, ContentHeight(window)));
        Assert.Equal("20°", Named<TextBlock>(window, "WeatherTemp")!.Text);
        Assert.Equal("Partly cloudy", Named<TextBlock>(window, "WeatherCondition")!.Text);
        Assert.NotNull(Named<ConditionIcon>(window, "WeatherIcon"));
        Assert.Null(Named<StackPanel>(window, "WeatherHourly"));
        Assert.Null(Named<TextBlock>(window, "WeatherFeelsLike"));
    }

    [AvaloniaFact]
    public void Medium_AddsTheHourlyStrip_ButNotTheLargeDetails()
    {
        var (window, _) = WindowWithWeather(0.6);
        var tier = WeatherLayout.SizeTier(0, ContentHeight(window));
        Assert.Equal(WeatherSizeTier.Medium, tier);
        var view = window.GetVisualDescendants().OfType<WeatherWidgetView>().Single();
        var strip = Named<StackPanel>(window, "WeatherHourly")!;
        Assert.Equal(WeatherLayout.HourlyCount(view.Bounds.Width - view.Padding.Left - view.Padding.Right), strip.Children.Count);
        Assert.True(strip.Children.Count > 0);
        Assert.Null(Named<TextBlock>(window, "WeatherFeelsLike"));
        UiSnapshot.Render(window, "weather-widget");
    }

    [AvaloniaFact]
    public void HourlyStrip_ShowsTwelveHourLabels_WhenTwelveHourIsSet()
    {
        var (window, main) = WindowWithWeather(0.6);
        main.Settings.UseTwelveHour = true;
        Dispatcher.UIThread.RunJobs();

        var labels = Named<StackPanel>(window, "WeatherHourly")!.Children
            .Select(cell => ((TextBlock)((StackPanel)cell).Children[0]).Text).ToList();
        Assert.Equal("10 AM", labels[0]);
        Assert.Equal("11 AM", labels[1]);
        UiSnapshot.Render(window, "weather-hour-format");
    }

    [AvaloniaFact]
    public void Backdrop_ShowsWhenReady_AndNotWhileLoading()
    {
        var (window, main) = WindowWithWeather(0.6);
        var view = window.GetVisualDescendants().OfType<WeatherWidgetView>().Single();
        Assert.IsType<Avalonia.Media.LinearGradientBrush>(view.Background);

        main.Weather.Show(WeatherState.Loading, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(view.Background);
    }

    [AvaloniaFact]
    public void Large_ShowsFeelsLikeHighLowAndFooter()
    {
        var (window, _) = WindowWithWeather(1000);
        Assert.Equal(WeatherSizeTier.Large, WeatherLayout.SizeTier(0, ContentHeight(window)));
        Assert.Equal("Feels like 18°", Named<TextBlock>(window, "WeatherFeelsLike")!.Text);
        Assert.Equal("High 24°  ·  Low 12°", Named<TextBlock>(window, "WeatherHighLow")!.Text);
        Assert.NotNull(Named<TextBlock>(window, "WeatherFooter"));
    }

    [AvaloniaTheory]
    [InlineData(WeatherState.Loading, "Loading weather…")]
    [InlineData(WeatherState.NoLocation, "Set a location to see the weather.")]
    [InlineData(WeatherState.Offline, "Weather is offline.")]
    public void NonReadyStates_ShowAShortMessage(WeatherState state, string message)
    {
        var (window, _) = WindowWithWeather(1, state);
        Assert.Equal(message, Named<TextBlock>(window, "WeatherMessage")!.Text);
        Assert.Null(Named<TextBlock>(window, "WeatherTemp"));
    }

    [AvaloniaFact]
    public void Offline_ShowsWhenItWasLastUpdated()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.Weather.Show(WeatherState.Offline, Sample, UiSnapshot.SampleNow.AddSeconds(-425));
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Weather, 0, layout.Columns[0].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Updated 7 min ago", Named<TextBlock>(window, "WeatherUpdated")!.Text);
    }
}
