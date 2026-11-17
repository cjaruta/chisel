using Chisel.App.Controls;

namespace Chisel.App.Tests.Controls;

// How a column's height (or the window's width) is shared out by weight, never below a widget's minimum.
public class WeightedStackPanelTests
{
    private static double[] Split(double available, double spacing, params (double Weight, double Min)[] items) =>
        WeightedStackPanel.Distribute(available, spacing, items);

    [Fact]
    public void SharesSpaceByWeight()
    {
        Assert.Equal(new[] { 100.0, 200.0 }, Split(300, 0, (1, 0), (2, 0)));
    }

    [Fact]
    public void SpacingBetweenItemsIsTakenOutFirst()
    {
        Assert.Equal(new[] { 100.0, 100.0, 100.0 }, Split(332, 16, (1, 0), (1, 0), (1, 0)));
    }

    [Fact]
    public void AnItemBelowItsMinimum_GetsTheMinimumAndTheRestShareWhatIsLeft()
    {
        Assert.Equal(new[] { 120.0, 160.0, 320.0 }, Split(600, 0, (0.1, 120), (1, 0), (2, 0)));
    }

    [Fact]
    public void MinimumsThatPushOthersBelowTheirs_AreAllHonoured()
    {
        // 300 by weight would be 30/90/180; the first two then need their minimums and the third takes the rest.
        Assert.Equal(new[] { 100.0, 100.0, 100.0 }, Split(300, 0, (1, 100), (3, 100), (6, 0)));
    }

    [Fact]
    public void WhenMinimumsDoNotFit_EachItemGetsItsMinimum()
    {
        Assert.Equal(new[] { 150.0, 150.0 }, Split(200, 0, (1, 150), (1, 150)));
    }

    [Fact]
    public void NoSpace_GivesMinimums()
    {
        Assert.Equal(new[] { 10.0, 0.0 }, Split(0, 16, (1, 10), (1, 0)));
    }

    [Fact]
    public void NoItems_GivesNothing()
    {
        Assert.Empty(Split(300, 16));
    }
}
