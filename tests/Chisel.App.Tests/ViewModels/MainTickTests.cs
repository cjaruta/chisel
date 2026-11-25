using Chisel.App.ViewModels;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.ViewModels;

public class MainTickTests
{
    [Fact]
    public void Visible_TicksGently_OrFastForARunningStopwatch()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(200), MainTick.Interval(windowHidden: false, stopwatchRunning: false));
        Assert.Equal(TimeSpan.FromMilliseconds(ClockToolsFormat.StopwatchStepMilliseconds), MainTick.Interval(false, true));
    }

    // Minimized or waiting in the tray, nothing is on screen: once a second still fires alarms and saves on time.
    [Fact]
    public void Hidden_TicksOncePerSecond_EvenWithAStopwatchRunning()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), MainTick.Interval(windowHidden: true, stopwatchRunning: false));
        Assert.Equal(TimeSpan.FromSeconds(1), MainTick.Interval(windowHidden: true, stopwatchRunning: true));
    }
}
