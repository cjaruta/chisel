using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.Composition;
using Chisel.Core.Weather;

namespace Chisel.App.Views.Widgets.Backdrop;

// Draws the motion on Avalonia's render thread in one pass. Brushes and the pen are built once per scene; each frame
// only reads positions from WeatherMotion and draws, so steady state allocates nothing and never touches layout.
internal sealed class WeatherMotionHandler : CompositionCustomVisualHandler
{
    internal sealed record Scene(MotionStyle Style);
    internal sealed record Run(bool On);

    private const double CornerRadius = 12;
    private MotionStyle _style;
    private IImmutableBrush? _solid;
    private IImmutableBrush? _soft;
    private ImmutablePen? _pen;
    private bool _running;
    private double _lastDrawn;
    private double _interval = 1.0 / 8;

    public override void OnMessage(object message)
    {
        switch (message)
        {
            case Scene scene:
                _style = scene.Style;
                var color = Color.FromUInt32(0xFF000000 | _style.Color);
                _solid = new ImmutableSolidColorBrush(color);
                _soft = new ImmutableRadialGradientBrush(new[] { new ImmutableGradientStop(0, color), new ImmutableGradientStop(1, Color.FromArgb(0, color.R, color.G, color.B)) }, radius: 0.5);
                _pen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1.2, lineCap: PenLineCap.Round);
                _interval = 1.0 / WeatherMotion.Fps(_style.Kind);
                Invalidate();
                break;
            case Run run:
                if (run.On && !_running)
                {
                    _running = true;
                    RegisterForNextAnimationFrameUpdate();
                }
                _running = run.On;
                Invalidate();
                break;
        }
    }

    // Called once per display frame while registered; redraws only when the scene's own rate says so, and stops
    // asking for frames entirely when not running.
    public override void OnAnimationFrameUpdate()
    {
        if (!_running)
        {
            return;
        }
        var now = WeatherMotion.Now;
        if (now - _lastDrawn >= _interval)
        {
            _lastDrawn = now;
            Invalidate();
        }
        RegisterForNextAnimationFrameUpdate();
    }

    public override void OnRender(ImmediateDrawingContext context)
    {
        var bounds = GetRenderBounds();
        if (!_running || _solid is null || _soft is null || _pen is null || bounds.Width < 1 || bounds.Height < 1)
        {
            return;
        }
        using var clip = context.PushClip(new RoundedRect(bounds, CornerRadius));
        var t = WeatherMotion.Now;
        var kind = _style.Kind;
        var count = WeatherMotion.Count(kind, bounds.Width, bounds.Height);
        for (var i = 0; i < count; i++)
        {
            var p = WeatherMotion.Get(kind, i, t, bounds.Width, bounds.Height);
            using var fade = context.PushOpacity(p.Opacity * _style.MaxOpacity, bounds);
            switch (kind)
            {
                case MotionKind.Rain:
                case MotionKind.HeavyRain:
                    context.DrawLine(_pen, new Point(p.X, p.Y), new Point(p.X, p.Y + p.Size));
                    break;
                case MotionKind.Snow:
                case MotionKind.Stars:
                    context.DrawEllipse(_solid, null, new Point(p.X, p.Y), p.Size, p.Size);
                    break;
                case MotionKind.Clouds:
                    context.DrawEllipse(_soft, null, new Point(p.X, p.Y), p.Size, p.Size * 0.35);
                    break;
                case MotionKind.Fog:
                    context.DrawEllipse(_soft, null, new Point(p.X, p.Y), p.Size, p.Size * 0.12);
                    break;
                case MotionKind.SunGlow:
                    context.DrawEllipse(_soft, null, new Point(p.X, p.Y), p.Size, p.Size);
                    break;
            }
        }
    }
}
