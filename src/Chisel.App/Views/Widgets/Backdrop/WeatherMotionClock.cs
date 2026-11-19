using System;
using System.Collections.Generic;
using Avalonia.Threading;
using Chisel.Core.Weather;

namespace Chisel.App.Views.Widgets.Backdrop;

// One shared timer for every animating weather widget, instead of asking the compositor for a frame on every display
// refresh (that ran at the monitor rate, 165 times a second, and cost far more than the drawing). It ticks at 24 Hz only
// while some layer is animating, and each layer is told to redraw at its own scene rate (12 or 8 per second).
public static class WeatherMotionClock
{
    private static readonly List<WeatherMotionLayer> Layers = [];
    private static DispatcherTimer? _timer;

    public static void Add(WeatherMotionLayer layer)
    {
        if (!Layers.Contains(layer))
        {
            Layers.Add(layer);
        }
        if (_timer is null)
        {
            _timer = new DispatcherTimer(TimeSpan.FromSeconds(1.0 / 24), DispatcherPriority.Render, (_, _) => Tick());
        }
        _timer.Start();
    }

    public static void Remove(WeatherMotionLayer layer)
    {
        Layers.Remove(layer);
        if (Layers.Count == 0)
        {
            _timer?.Stop();
        }
    }

    public static bool IsTicking => _timer?.IsEnabled == true;

    private static void Tick()
    {
        var now = WeatherMotion.Now;
        for (var i = 0; i < Layers.Count; i++)
        {
            Layers[i].Tick(now);
        }
    }
}
