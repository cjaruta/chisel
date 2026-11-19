using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Chisel.App.ViewModels;
using Chisel.Core.Weather;

namespace Chisel.App.Views.Widgets.Backdrop;

// Ambient motion for the weather widget (stage 2 of the backdrop). It draws through one composition custom visual on the
// widget, so there is no timer, no layout change and no pointer handling. It runs only while the widget is attached and
// visible, the weather is Ready, the window is not minimized and the dashboard is not in edit mode; otherwise it stops
// asking for frames. Turn it off with WeatherMotion.MotionEnabled; remove it with the backdrop (see PROJECT_STATE.md).
public sealed class WeatherMotionLayer
{
    private static readonly ConditionalWeakTable<UserControl, WeatherMotionLayer> Layers = new();

    private readonly UserControl _host;
    private MainViewModel? _main;
    private Window? _window;
    private CompositionCustomVisual? _visual;
    private string? _sceneKey;

    private WeatherMotionLayer(UserControl host) => _host = host;

    public bool IsAnimating { get; private set; }

    public MotionKind Kind { get; private set; }

    public static WeatherMotionLayer? Of(UserControl host) => Layers.TryGetValue(host, out var layer) ? layer : null;

    public static void Install(UserControl host)
    {
#pragma warning disable CS0162 // MotionEnabled is a constant switch.
        if (!WeatherMotion.MotionEnabled)
        {
            return;
        }
#pragma warning restore CS0162
        var layer = new WeatherMotionLayer(host);
        Layers.Add(host, layer);
        host.AttachedToVisualTree += (_, _) => layer.Attach();
        host.DetachedFromVisualTree += (_, _) => layer.Detach();
    }

    private void Attach()
    {
        if (_host.DataContext is MainViewModel main)
        {
            _main = main;
            main.Weather.PropertyChanged += OnChanged;
            main.Layout.PropertyChanged += OnChanged;
        }
        _window = TopLevel.GetTopLevel(_host) as Window;
        if (_window is not null)
        {
            _window.PropertyChanged += OnWindowChanged;
        }
        _host.PropertyChanged += OnHostChanged;
        _host.SizeChanged += OnSizeChanged;

        var compositor = ElementComposition.GetElementVisual(_host)?.Compositor;
        if (compositor is not null)
        {
            _visual = compositor.CreateCustomVisual(new WeatherMotionHandler());
            _visual.Size = new Vector2((float)_host.Bounds.Width, (float)_host.Bounds.Height);
            ElementComposition.SetElementChildVisual(_host, _visual);
        }
        Update();
    }

    private void Detach()
    {
        if (_main is not null)
        {
            _main.Weather.PropertyChanged -= OnChanged;
            _main.Layout.PropertyChanged -= OnChanged;
            _main = null;
        }
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowChanged;
            _window = null;
        }
        _host.PropertyChanged -= OnHostChanged;
        _host.SizeChanged -= OnSizeChanged;
        _visual?.SendHandlerMessage(new WeatherMotionHandler.Run(false));
        _visual = null;
        _sceneKey = null;
        IsAnimating = false;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void OnWindowChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            Update();
        }
    }

    private void OnHostChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty)
        {
            Update();
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_visual is not null)
        {
            _visual.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        }
    }

    private void Update()
    {
        var weather = _main?.Weather;
        var run = _main is not null
            && _host.IsEffectivelyVisible
            && !_main.Layout.IsEditing
            && _window?.WindowState != WindowState.Minimized
            && weather is { State: WeatherState.Ready, Snapshot: not null };
        var key = run ? WeatherBackdrop.Key(weather!.Snapshot!.Condition, weather.Snapshot.IsDay) : null;
        if (key != _sceneKey)
        {
            _sceneKey = key;
            if (key is not null)
            {
                var style = WeatherMotion.Style(key);
                Kind = style.Kind;
                _visual?.SendHandlerMessage(new WeatherMotionHandler.Scene(style));
            }
        }
        if (run != IsAnimating)
        {
            IsAnimating = run;
            _visual?.SendHandlerMessage(new WeatherMotionHandler.Run(run));
        }
    }
}
