using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Weather;
using Xunit;

namespace Chisel.App.Tests.Headless;

public class WeatherSettingsHeadlessTests
{
    private sealed class FakeGeocoder(IReadOnlyList<GeocodingResult> results) : IWeatherGeocoder
    {
        public string? LastSearch;

        public Task<IReadOnlyList<GeocodingResult>> SearchAsync(string name, CancellationToken cancellationToken = default)
        {
            LastSearch = name;
            return Task.FromResult(results);
        }
    }

    private static T Named<T>(Chisel.App.Views.MainWindow w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    [AvaloniaFact]
    public async Task WeatherPage_TypingShowsSuggestions_AndPickingOneSavesIt()
    {
        var geocoder = new FakeGeocoder(
        [
            new GeocodingResult("Testville", "Test Region", "Testland", 12.3456, -45.6789),
            new GeocodingResult("Testville", null, "Otherland", 1.0, 2.0),
        ]);
        var store = new MemoryWeatherStore();
        var window = UiSnapshot.ShowMainWindow(geocoder: geocoder, weatherStore: store);
        var main = (MainViewModel)window.DataContext!;
        main.Settings.IsOpen = true;
        main.Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        main.Settings.SelectedWidgetPage = main.Settings.WidgetPages.Single(p => p.Id == "weather");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Weather data by Open-Meteo", Named<TextBlock>(window, "WeatherCredit").Text);
        Assert.Equal("No location set", Named<TextBlock>(window, "WeatherChosen").Text);

        var box = Named<AutoCompleteBox>(window, "WeatherSearchBox");
        box.Text = "Testville";
        for (var i = 0; i < 100 && (box.ItemsSource as IEnumerable<object>)?.Any() != true; i++)
        {
            await Task.Delay(50);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal("Testville", geocoder.LastSearch);
        var shown = ((IEnumerable<object>)box.ItemsSource!).Cast<GeocodingResult>().Select(r => r.DisplayText).ToList();
        Assert.Equal(["Testville, Test Region, Testland", "Testville, Otherland"], shown);
        UiSnapshot.Render(window, "settings-weather");

        box.SelectedItem = box.ItemsSource!.Cast<object>().First();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new WeatherLocation("Testville", 12.35, -45.68), store.GetLocation());
        Assert.Equal("Testville", Named<TextBlock>(window, "WeatherChosen").Text);
        Assert.Equal("", box.Text);

        Named<Button>(window, "WeatherClearButton").Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(store.GetLocation());
        Assert.Equal("No location set", Named<TextBlock>(window, "WeatherChosen").Text);
        window.Close();
    }

    [AvaloniaFact]
    public void WeatherPage_TemperatureToggle_ChangesTheWidgetText()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Weather, 0, layout.Columns[0].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        main.Weather.Show(WeatherState.Ready, new WeatherSnapshot(20, 18, WeatherCondition.Clear, 24, 12, []));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("20°", Named<TextBlock>(window, "WeatherTemp").Text);

        main.Settings.IsOpen = true;
        main.Settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        main.Settings.SelectedWidgetPage = main.Settings.WidgetPages.Single(p => p.Id == "weather");
        Dispatcher.UIThread.RunJobs();
        Named<RadioButton>(window, "WeatherFahrenheit").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("68°", Named<TextBlock>(window, "WeatherTemp").Text);
        Assert.True(Named<RadioButton>(window, "WeatherCelsius").IsChecked == false);
        UiSnapshot.Render(window, "settings-weather-unit");
        window.Close();
    }
}
