using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Chisel.Core.Weather;

namespace Chisel.App.Views.Widgets.Backdrop;

// Runs the motion on Avalonia's render thread. It only schedules frames (at the scene's own rate, and not at all when
// stopped) and hands the drawing to WeatherMotionDrawing.Draw, the same method the offscreen render uses.
internal sealed class WeatherMotionHandler : CompositionCustomVisualHandler
{
    internal sealed record Scene(string Key);
    internal sealed record Run(bool On);

    private WeatherMotionDrawing.Scene? _scene;
    private bool _running;
    private double _lastDrawn;
    private double _interval = 1.0 / 8;

    public override void OnMessage(object message)
    {
        switch (message)
        {
            case Scene scene:
                _scene = WeatherMotionDrawing.SceneFor(scene.Key);
                _interval = 1.0 / WeatherMotion.Fps(_scene.Style.Kind);
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
        if (!_running || _scene is null)
        {
            return;
        }
        using var clip = context.PushClip(new Rect(bounds.Size));
        WeatherMotionDrawing.Draw(new WeatherMotionDrawing.ImmediateSurface(context), _scene, bounds.Size, WeatherMotion.Now);
    }
}
