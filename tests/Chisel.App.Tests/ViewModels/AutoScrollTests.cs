using Chisel.App.ViewModels;

namespace Chisel.App.Tests.ViewModels;

// How far the grid scrolls per tick while dragging near or past the top or bottom of the scroll area.
public class AutoScrollTests
{
    private const double Viewport = 500;

    [Theory]
    [InlineData(250)]
    [InlineData(ScheduleGridLayout.AutoScrollEdgePixels)]
    [InlineData(Viewport - ScheduleGridLayout.AutoScrollEdgePixels)]
    public void AwayFromTheEdges_DoesNotScroll(double y)
    {
        Assert.Equal(0, ScheduleGridLayout.AutoScrollStep(y, Viewport));
    }

    [Fact]
    public void NearTheTop_ScrollsUpMoreTheCloserItGets()
    {
        var near = ScheduleGridLayout.AutoScrollStep(ScheduleGridLayout.AutoScrollEdgePixels / 2, Viewport);
        var closer = ScheduleGridLayout.AutoScrollStep(2, Viewport);

        Assert.True(near < 0);
        Assert.True(closer < near);
    }

    [Fact]
    public void NearTheBottom_ScrollsDown()
    {
        Assert.True(ScheduleGridLayout.AutoScrollStep(Viewport - 5, Viewport) > 0);
    }

    [Theory]
    [InlineData(-300, -1)]
    [InlineData(Viewport + 300, 1)]
    public void PastTheEdge_ScrollsAtMostTheMaximumStep(double y, int direction)
    {
        Assert.Equal(direction * ScheduleGridLayout.AutoScrollMaxStepPixels, ScheduleGridLayout.AutoScrollStep(y, Viewport));
    }
}
