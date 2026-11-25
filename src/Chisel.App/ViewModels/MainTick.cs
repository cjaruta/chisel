using System;
using Chisel.Core.Clock;

namespace Chisel.App.ViewModels;

// How often the main window ticks the dashboard: gently while visible, fast enough for every tenth while a stopwatch runs,
// and once a second while minimized or in the tray (nothing is drawn, but alarms, timers and saves stay on time).
public static class MainTick
{
    public static TimeSpan Interval(bool windowHidden, bool stopwatchRunning) =>
        windowHidden ? TimeSpan.FromSeconds(1)
        : stopwatchRunning ? TimeSpan.FromMilliseconds(ClockToolsFormat.StopwatchStepMilliseconds)
        : TimeSpan.FromMilliseconds(200);
}
