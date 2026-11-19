using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Chisel.App.ViewModels;
using Chisel.Core.Weather;

namespace Chisel.App.Views.Widgets.Backdrop;

// Static weather backdrop: paints the scene gradient as the widget's background, behind its content, with rounded
// corners. It takes no space and no pointer input, and shows nothing unless the weather is Ready.
// Remove it by deleting this folder, WeatherBackdrop.cs in Core, and the one Install line in WeatherWidgetView.
public sealed class WeatherBackdropLayer
{
    private readonly UserControl _host;
    private WeatherViewModel? _weather;

    private WeatherBackdropLayer(UserControl host) => _host = host;

    public static void Install(UserControl host)
    {
        var layer = new WeatherBackdropLayer(host);
        host.CornerRadius = new CornerRadius(12);
        host.AttachedToVisualTree += (_, _) =>
        {
            if (host.DataContext is MainViewModel main)
            {
                layer._weather = main.Weather;
                layer._weather.PropertyChanged += layer.OnChanged;
            }
            layer.Update();
        };
        host.DetachedFromVisualTree += (_, _) =>
        {
            if (layer._weather is not null)
            {
                layer._weather.PropertyChanged -= layer.OnChanged;
                layer._weather = null;
            }
        };
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update()
    {
        if (_weather is not { State: WeatherState.Ready, Snapshot: { } snapshot }
            || !WeatherBackdrop.Gradients.TryGetValue(WeatherBackdrop.Key(snapshot.Condition, snapshot.IsDay), out var gradient))
        {
            _host.Background = null;
            return;
        }
        _host.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromUInt32(0xFF000000 | gradient.Top), 0),
                new GradientStop(Color.FromUInt32(0xFF000000 | gradient.Bottom), 1),
            },
        };
    }
}
