using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

public class ClockSlotAllocationTests
{
    private static ClockSlotsConfig ClockAnd(int tools)
    {
        var slots = new List<ClockSlotConfig> { ClockSlotConfig.Pinned(ClockTool.Clock) };
        slots.AddRange(new[] { ClockSlotConfig.Scrolling(ClockTool.Timer, ClockTool.Stopwatch), ClockSlotConfig.Pinned(ClockTool.Alarm) }.Take(tools));
        return new ClockSlotsConfig(slots);
    }

    private static double[] Extents(ClockSlotsConfig config, double width, double height) =>
        ClockSlotLayout.Plan(config, width, height).Slots.Select(s => s.Extent).ToArray();

    [Theory]
    [InlineData(640, 300, 300, 340)] // the clock gets a square, the tool the rest
    [InlineData(480, 300, 300, 180)] // half a screen: still a square clock, a narrower tool
    [InlineData(400, 300, 240, 160)] // tighter: the clock gives way so the tool keeps its minimum
    [InlineData(1200, 310, 310, 890)] // wide: no unused space beside the clock
    public void TheClockTakesTheWidthItNeedsForItsHeight_AndTheToolGetsTheRest(double width, double height, double clock, double tool)
    {
        var extents = Extents(ClockAnd(1), width, height);
        Assert.Equal([clock, tool], extents);
        Assert.Equal(width, extents.Sum());
    }

    [Fact]
    public void ATallWidget_StacksThemAndTheClockIsASquareOfTheWidth()
    {
        var extents = Extents(ClockAnd(1), 300, 640);
        Assert.Equal([300, 340], extents);
    }

    [Theory]
    [InlineData(900, 300, 300, 300)]
    [InlineData(700, 300, 300, 200)]
    [InlineData(500, 300, 180, 160)] // 500 fits three at the minimum only by shrinking the clock
    public void WithTwoToolSlots_TheyShareTheRemainingSpaceEqually(double width, double height, double clock, double each)
    {
        var plan = ClockSlotLayout.Plan(ClockAnd(2), width, height);
        Assert.Equal(3, plan.Slots.Count);
        Assert.Equal(clock, plan.Slots[0].Extent);
        Assert.Equal(each, plan.Slots[1].Extent);
        Assert.Equal(each, plan.Slots[2].Extent);
        Assert.Equal(width, plan.Slots.Sum(s => s.Extent));
    }

    [Fact]
    public void OnlySlotsThatAreOnTakeSpace_NothingIsHeldBackForTheOthers()
    {
        var one = Extents(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock)]), 640, 300);
        Assert.Equal([640d], one);

        var two = Extents(ClockAnd(1), 640, 300);
        Assert.Equal(2, two.Length);
    }

    [Fact]
    public void WithNoClockSlot_TheSlotsShareTheWidgetEqually()
    {
        var config = new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Timer), ClockSlotConfig.Pinned(ClockTool.Stopwatch)]);
        Assert.Equal([320d, 320d], Extents(config, 640, 300));
    }

    [Fact]
    public void ASlotThatScrollsThroughTheClockAmongOtherTools_IsShareLikeAnyToolSlot()
    {
        var config = new ClockSlotsConfig([ClockSlotConfig.Scrolling(ClockTool.Clock, ClockTool.Timer), ClockSlotConfig.Pinned(ClockTool.Alarm)]);
        Assert.Equal([320d, 320d], Extents(config, 640, 300));
    }

    [Fact]
    public void TwoPinnedClocks_EachTakeASquareAndShareWhatIsLeftWithTheTools()
    {
        var config = new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Timer)]);
        Assert.Equal([200d, 200d, 240d], Extents(config, 640, 200));
    }

    [Fact]
    public void TheAbsorbedSlotRuleStillHolds_AndTheExtentsStillFillTheWidget()
    {
        var config = new ClockSlotsConfig([
            ClockSlotConfig.Pinned(ClockTool.Clock),
            ClockSlotConfig.Pinned(ClockTool.Timer),
            ClockSlotConfig.Scrolling(ClockTool.Stopwatch, ClockTool.Alarm)]);

        var plan = ClockSlotLayout.Plan(config, 330, 300); // room for two
        Assert.Equal(2, plan.Slots.Count);
        Assert.Equal([ClockTool.Timer, ClockTool.Stopwatch, ClockTool.Alarm], plan.Slots[1].Tools);
        Assert.Equal(330, plan.Slots.Sum(s => s.Extent));
        Assert.All(plan.Slots, s => Assert.True(s.Extent >= ClockSlotLayout.MinSlotExtent - 0.001));
    }

    [Theory]
    [InlineData(150, 150)]
    [InlineData(200, 700)]
    [InlineData(333, 211)]
    [InlineData(1920, 300)]
    public void TheExtentsAlwaysAddUpToTheRoomAlongTheSlots(double width, double height)
    {
        var plan = ClockSlotLayout.Plan(ClockAnd(2), width, height);
        var room = plan.Horizontal ? width : height;
        Assert.Equal(room, plan.Slots.Sum(s => s.Extent), 6);
    }
}
