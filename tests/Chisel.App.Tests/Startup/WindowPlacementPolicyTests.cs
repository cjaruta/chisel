using Avalonia;
using Avalonia.Controls;
using Chisel.App.Startup;

namespace Chisel.App.Tests.Startup;

public class WindowPlacementPolicyTests
{
    private static readonly PixelRect[] OneScreen = { new(0, 0, 1920, 1040) };

    [Fact]
    public void ForSave_WhenNormal_ReturnsThePlacement()
    {
        var placement = WindowPlacementPolicy.ForSave(WindowState.Normal, 1000, 640, new PixelPoint(100, 50));

        Assert.Equal(new WindowPlacement(1000, 640, 100, 50), placement);
    }

    [Theory]
    [InlineData(WindowState.Minimized)]
    [InlineData(WindowState.Maximized)]
    [InlineData(WindowState.FullScreen)]
    public void ForSave_WhenNotNormal_ReturnsNullSoThePreviousPlacementIsKept(WindowState state)
    {
        Assert.Null(WindowPlacementPolicy.ForSave(state, 1000, 640, new PixelPoint(-32000, -32000)));
    }

    [Fact]
    public void IsOnScreen_PositionInsideAScreen_IsTrue()
    {
        Assert.True(WindowPlacementPolicy.IsOnScreen(new WindowPlacement(1000, 640, 100, 50), OneScreen));
    }

    [Fact]
    public void IsOnScreen_MinimizedSentinelPosition_IsFalse()
    {
        Assert.False(WindowPlacementPolicy.IsOnScreen(new WindowPlacement(160, 28, -32000, -32000), OneScreen));
    }

    [Fact]
    public void IsOnScreen_PositionOnAMonitorThatIsGone_IsFalse()
    {
        Assert.False(WindowPlacementPolicy.IsOnScreen(new WindowPlacement(1000, 640, 2500, 100), OneScreen));
    }

    [Fact]
    public void IsOnScreen_SecondMonitor_IsTrue()
    {
        var twoScreens = new[] { OneScreen[0], new PixelRect(1920, 0, 1920, 1040) };

        Assert.True(WindowPlacementPolicy.IsOnScreen(new WindowPlacement(1000, 640, 2500, 100), twoScreens));
    }
}
