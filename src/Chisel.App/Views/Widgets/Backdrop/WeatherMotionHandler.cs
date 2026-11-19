using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Chisel.Core.Weather;

namespace Chisel.App.Views.Widgets.Backdrop;

// Runs the motion on Avalonia's render thread. It redraws only when told to (WeatherMotionClock ticks, at the scene's own
// rate) and hands the drawing to WeatherMotionDrawing.Draw, the same method the offscreen render uses.
internal sealed class WeatherMotionHandler : CompositionCustomVisualHandler
{
    internal sealed record Scene(string Key);
    internal sealed record Run(bool On);

    // Sent by WeatherMotionClock when this scene is due a redraw.
    internal static readonly object TickMessage = new();

    private WeatherMotionDrawing.Scene? _scene;
    private bool _running;

    public override void OnMessage(object message)
    {
        switch (message)
        {
            case Scene scene:
                _scene = WeatherMotionDrawing.SceneFor(scene.Key);
                Invalidate();
                break;
            case Run run:
                if (run.On != _running)
                {
                    WeatherMotionStats.RunningChanged(run.On);
                }
                _running = run.On;
                Invalidate();
                break;
            default:
                if (ReferenceEquals(message, TickMessage) && _running)
                {
                    WeatherMotionStats.Callback();
                    Invalidate();
                }
                break;
        }
    }

    public override void OnRender(ImmediateDrawingContext context)
    {
        var bounds = GetRenderBounds();
        if (!_running || _scene is null)
        {
            return;
        }
        var started = Stopwatch.GetTimestamp();
        using (context.PushClip(new Rect(bounds.Size)))
        {
            WeatherMotionDrawing.Draw(new WeatherMotionDrawing.ImmediateSurface(context), _scene, bounds.Size, WeatherMotion.Now);
        }
        WeatherMotionStats.Rendered(started);
    }
}
