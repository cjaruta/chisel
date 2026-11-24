using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.Core.Theme;
using Chisel.Core.Weather;

namespace Chisel.App.Tests.Headless;

// The light mode on screen: soft surfaces, and the text that sits on the plain card is theme text that reads on it.
public class LightModeHeadlessTests
{
    private static Rgb Of(IBrush? brush) => brush is ISolidColorBrush { Color: var c } ? new Rgb(c.R, c.G, c.B) : new Rgb(0, 0, 0);

    [AvaloniaTheory]
    [InlineData(WeatherState.Loading)]
    [InlineData(WeatherState.NoLocation)]
    [InlineData(WeatherState.Offline)]
    public void TheWeatherMessages_ReadOnTheLightCard(WeatherState state)
    {
        var window = UiSnapshot.ShowMainWindow(themeMode: "light");
        var main = (MainViewModel)window.DataContext!;
        main.Weather.Show(state, null);
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Weather, 0, layout.Columns[0].Widgets.Count, 1));
        Dispatcher.UIThread.RunJobs();
        var light = ThemeModes.ById("light");

        var message = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "WeatherMessage");
        Assert.True(Rgb.Contrast(Of(message.Foreground), light.Card) >= 7, "the weather message is hard to read on the light card");
        window.Close();
    }

    [AvaloniaFact]
    public void TheLightDashboard_IsRenderedAsSoftSurfaces_NotWhite()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 1500, themeMode: "light");
        var light = ThemeModes.ById("light");

        Assert.Equal(light.Window, Of((IBrush?)window.FindResource("WindowBackgroundBrush")));
        Assert.Equal(light.Card, Of((IBrush?)window.FindResource("CardBackgroundBrush")));
        UiSnapshot.Render(window, "light-mode-tall");
        window.Close();
    }
}
