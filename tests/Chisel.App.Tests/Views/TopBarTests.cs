using Chisel.App.Views;

namespace Chisel.App.Tests.Views;

public class TopBarTests
{
    [Theory]
    [InlineData(1400, 36, 20)]
    [InlineData(899, 30, 17)]
    [InlineData(599, 26, 15)]
    public void TheClock_ShrinksOnNarrowerWindows(double width, double time, double date) =>
        Assert.Equal((time, date), TopBar.ClockFontSizes(width));

    [Theory]
    [InlineData(1400, 460, 400, 150, ToolbarPlacement.Centered)]
    [InlineData(1100, 460, 400, 150, ToolbarPlacement.BetweenClockAndButtons)] // centered would cover the clock
    [InlineData(900, 460, 400, 150, ToolbarPlacement.OwnLine)]
    [InlineData(1400, 0, 400, 150, ToolbarPlacement.OwnLine)]
    public void TheToolbar_GoesWhereItFits(double width, double toolbar, double left, double right, ToolbarPlacement expected) =>
        Assert.Equal(expected, TopBar.PlaceToolbar(width, toolbar, left, right));
}
