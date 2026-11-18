using System.Linq;
using Chisel.App.ViewModels;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class WeatherWidgetLayoutTests
{
    [Fact]
    public void WeatherIsRegisteredButHiddenByDefault()
    {
        Assert.NotNull(WidgetRegistry.Get(WidgetRegistry.Weather));
        Assert.DoesNotContain(WidgetRegistry.Weather, WidgetRegistry.DefaultLayout.WidgetIds);
        Assert.Contains(WidgetRegistry.Weather, WidgetRegistry.DefaultLayout.HiddenWidgetIds);
    }

    [Fact]
    public void AddingAndRemovingWeather_LeavesTheOtherLayoutAlone()
    {
        var viewModel = new LayoutViewModel();
        var before = viewModel.Layout.Describe();
        viewModel.EnterEditCommand.Execute(null);

        viewModel.AddWidgetFromTray(WidgetRegistry.Weather);
        var added = viewModel.Layout;
        viewModel.RemoveWidget(WidgetRegistry.Weather);

        Assert.Equal("weather", added.Columns[0].Widgets[^1].WidgetId);
        Assert.Equal(1, added.WidgetIds.Count(id => id == "weather"));
        Assert.Equal(before.Split(" (hidden")[0], viewModel.Layout.Describe().Split(" (hidden")[0]);
    }
}
