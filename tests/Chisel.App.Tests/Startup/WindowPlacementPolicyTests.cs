using Avalonia;
using Avalonia.Controls;
using Chisel.App.Startup;

namespace Chisel.App.Tests.Startup;

public class WindowPlacementPolicyTests
{
    private static readonly ScreenArea Primary = new(new PixelRect(0, 0, 1920, 1040), 1, IsPrimary: true);
    private static readonly ScreenArea RightHiDpi = new(new PixelRect(1920, 0, 3840, 2120), 2, IsPrimary: false);

    [Fact]
    public void Restore_OnTheSameScreen_KeepsPositionAndSize()
    {
        var plan = WindowPlacementPolicy.Restore(new WindowPlacement(1000, 640, 100, 50), [Primary])!;

        Assert.Equal(new PixelPoint(100, 50), plan.Position);
        Assert.Equal((1000, 640), (plan.Width, plan.Height));
        Assert.False(plan.Maximized);
    }

    [Fact]
    public void Restore_OnASecondMonitor_StaysThere()
    {
        var plan = WindowPlacementPolicy.Restore(new WindowPlacement(1000, 640, 2500, 100), [Primary, RightHiDpi])!;

        Assert.Equal(new PixelPoint(2500, 100), plan.Position);
    }

    [Fact]
    public void Restore_WhenTheMonitorWasUnplugged_CentersADefaultWindowOnThePrimaryScreen()
    {
        var plan = WindowPlacementPolicy.Restore(new WindowPlacement(1000, 640, 2500, 100, Maximized: true), [Primary])!;

        Assert.Equal((WindowPlacementPolicy.DefaultWidth, WindowPlacementPolicy.DefaultHeight), (plan.Width, plan.Height));
        Assert.Equal(new PixelPoint((1920 - 1200) / 2, (1040 - 760) / 2), plan.Position);
        Assert.False(plan.Maximized);
    }

    [Fact]
    public void Restore_PicksThePrimaryScreenEvenWhenItIsNotListedFirst()
    {
        var plan = WindowPlacementPolicy.Restore(null, [RightHiDpi with { IsPrimary = false }, Primary])!;

        Assert.True(plan.Position.X < 1920);
    }

    [Fact]
    public void Restore_ATinySize_IsRaisedToTheMinimum()
    {
        var plan = WindowPlacementPolicy.Restore(new WindowPlacement(160, 28, 100, 50), [Primary])!;

        Assert.Equal((WindowPlacementPolicy.MinWidth, WindowPlacementPolicy.MinHeight), (plan.Width, plan.Height));
    }

    [Fact]
    public void Restore_AfterTheResolutionShrank_FitsTheWindowOnTheScreen()
    {
        var small = new ScreenArea(new PixelRect(0, 0, 1280, 680), 1, IsPrimary: true);

        var plan = WindowPlacementPolicy.Restore(new WindowPlacement(1800, 1000, 900, 300), [small])!;

        Assert.Equal((1280, 680), (plan.Width, plan.Height));
        Assert.Equal(new PixelPoint(0, 0), plan.Position);
    }

    [Fact]
    public void Restore_OnAScreenWithHigherScaling_MeasuresTheSizeInThatScreensPixels()
    {
        // 150%: 1920x1040 pixels hold only 1280x693 device-independent units.
        var scaled = new ScreenArea(new PixelRect(0, 0, 1920, 1040), 1.5, IsPrimary: true);

        var plan = WindowPlacementPolicy.Restore(new WindowPlacement(1600, 900, 0, 0), [scaled])!;

        Assert.Equal(1280, plan.Width);
        Assert.Equal(1040 / 1.5, plan.Height, 3);
    }

    [Fact]
    public void Restore_AWindowHangingOffTheEdge_IsPulledBackOn()
    {
        var plan = WindowPlacementPolicy.Restore(new WindowPlacement(1000, 640, 1700, 900), [Primary])!;

        Assert.Equal(new PixelPoint(920, 400), plan.Position);
    }

    [Fact]
    public void Restore_OnlyATitleBarCornerVisible_CountsAsOffScreen()
    {
        var plan = WindowPlacementPolicy.Restore(new WindowPlacement(1000, 640, 1900, 1030), [Primary])!;

        Assert.Equal((WindowPlacementPolicy.DefaultWidth, WindowPlacementPolicy.DefaultHeight), (plan.Width, plan.Height));
    }

    [Fact]
    public void Restore_Maximized_IsKeptOnItsScreen()
    {
        var plan = WindowPlacementPolicy.Restore(new WindowPlacement(1000, 640, 2500, 100, Maximized: true), [Primary, RightHiDpi])!;

        Assert.True(plan.Maximized);
        Assert.Equal(new PixelPoint(2500, 100), plan.Position);
    }

    [Fact]
    public void Restore_WithoutAnyScreen_LeavesTheWindowAlone()
    {
        Assert.Null(WindowPlacementPolicy.Restore(new WindowPlacement(1000, 640, 100, 50), []));
    }

    [Fact]
    public void Tracker_KeepsTheNormalBoundsWhileMaximizedOrMinimized()
    {
        var tracker = new WindowPlacementTracker();

        tracker.Observe(WindowState.Normal, 1000, 640, new PixelPoint(100, 50));
        tracker.Observe(WindowState.Maximized, 1920, 1040, new PixelPoint(-8, -8));
        var maximized = tracker.Current;
        tracker.Observe(WindowState.Minimized, 160, 28, new PixelPoint(-32000, -32000));

        Assert.Equal(new WindowPlacement(1000, 640, 100, 50, Maximized: true), maximized);
        Assert.Equal(maximized, tracker.Current);
    }

    [Fact]
    public void Tracker_BeforeAnyNormalBounds_HasNothingToSave()
    {
        var tracker = new WindowPlacementTracker();

        tracker.Observe(WindowState.Minimized, 160, 28, new PixelPoint(-32000, -32000));

        Assert.Null(tracker.Current);
    }
}
