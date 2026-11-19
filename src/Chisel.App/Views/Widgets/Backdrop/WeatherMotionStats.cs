using System.Diagnostics;
using System.Threading;

namespace Chisel.App.Views.Widgets.Backdrop;

// Counters for the weather-motion capture (cpu.txt): how often the handler is called, how often it draws and for how
// long, and how many handlers are running at once. Interlocked adds only, so it costs nothing measurable.
public static class WeatherMotionStats
{
    private static long _callbacks;
    private static long _renders;
    private static long _renderTicks;
    private static int _running;

    public static long Callbacks => Interlocked.Read(ref _callbacks);
    public static long Renders => Interlocked.Read(ref _renders);
    public static double RenderMilliseconds => Interlocked.Read(ref _renderTicks) * 1000.0 / Stopwatch.Frequency;
    public static int Running => Volatile.Read(ref _running);

    internal static void Callback() => Interlocked.Increment(ref _callbacks);

    internal static void Rendered(long startTimestamp)
    {
        Interlocked.Increment(ref _renders);
        Interlocked.Add(ref _renderTicks, Stopwatch.GetTimestamp() - startTimestamp);
    }

    internal static void RunningChanged(bool on) => Interlocked.Add(ref _running, on ? 1 : -1);
}
