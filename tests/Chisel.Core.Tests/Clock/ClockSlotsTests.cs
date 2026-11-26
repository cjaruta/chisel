using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

public class ClockSlotsTests
{
    private static ClockSlotConfig Scroll(params ClockTool[] tools) => ClockSlotConfig.Scrolling(tools);

    [Fact]
    public void Default_IsTheClockPinnedInTheMainSlot_AndOneScrollingSlotWithEveryTool()
    {
        var slots = ClockSlotsConfig.Default.Slots;
        Assert.Equal(2, slots.Count);
        Assert.Equal([ClockTool.Clock], slots[0].Tools);
        Assert.Equal([ClockTool.Timer, ClockTool.Stopwatch, ClockTool.Alarm, ClockTool.Empty], slots[1].Tools);
        Assert.True(slots[1].IsScrolling);
        Assert.False(slots[0].IsScrolling);
    }

    [Fact]
    public void AScrollingSlot_ListsItsToolsInTheFixedOrder_AndAnEmptySetIsJustEmpty()
    {
        Assert.Equal([ClockTool.Clock, ClockTool.Alarm], Scroll(ClockTool.Alarm, ClockTool.Clock).Tools);
        Assert.Equal([ClockTool.Empty], new ClockSlotConfig(true, ClockTool.Timer, ClockToolSet.None).Tools);
    }

    [Fact]
    public void ASlotsConfig_KeepsBetweenOneAndThreeSlots()
    {
        Assert.Single(new ClockSlotsConfig([]).Normalized().Slots);
        var many = new ClockSlotsConfig(Enumerable.Repeat(ClockSlotConfig.Pinned(ClockTool.Timer), 5).ToList()).Normalized();
        Assert.Equal(3, many.Slots.Count);
    }

    [Fact]
    public void Configs_CompareByTheirSlots()
    {
        Assert.Equal(ClockSlotsConfig.Default, ClockSlotsConfig.Default with { });
        Assert.Equal(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock)]), new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock)]));
        Assert.NotEqual(ClockSlotsConfig.Default, new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock)]));
    }

    [Theory]
    [InlineData(600, 300, true)]
    [InlineData(300, 600, false)]
    [InlineData(400, 400, true)]
    public void TheOrientation_FollowsTheAspectRatio(double width, double height, bool horizontal) =>
        Assert.Equal(horizontal, ClockSlotLayout.IsHorizontal(width, height));

    [Theory]
    [InlineData(120, 300, 1)]
    [InlineData(360, 200, 2)]
    [InlineData(640, 300, 3)]
    [InlineData(1400, 300, 3)]
    [InlineData(200, 360, 2)] // tall: the height counts
    [InlineData(200, 100, 1)]
    public void Capacity_ComesFromTheLongSideOfTheWidget(double width, double height, int expected) =>
        Assert.Equal(expected, ClockSlotLayout.Capacity(width, height));

    [Fact]
    public void WhenEverySlotFits_TheyAreShownAsConfigured_InEqualCells()
    {
        var plan = ClockSlotLayout.Plan(ClockSlotsConfig.Default, 640, 300);
        Assert.True(plan.Horizontal);
        Assert.Equal(2, plan.Slots.Count);
        Assert.Equal([ClockTool.Clock], plan.Slots[0].Tools);
        Assert.Equal(ClockSlotsConfig.Default.Slots[1].Tools, plan.Slots[1].Tools);
        Assert.Equal(plan.Slots[0].Extent, plan.Slots[1].Extent); // equal cells: the same space around every item
    }

    [Fact]
    public void WhenSlotsDoNotFit_TheLastVisibleOneAbsorbsTheHiddenToolsIntoItsScrollSet()
    {
        var config = new ClockSlotsConfig([
            ClockSlotConfig.Pinned(ClockTool.Clock),
            ClockSlotConfig.Pinned(ClockTool.Timer),
            Scroll(ClockTool.Stopwatch, ClockTool.Alarm, ClockTool.Empty)]);

        var plan = ClockSlotLayout.Plan(config, 400, 300); // room for two
        Assert.Equal(2, plan.Slots.Count);
        Assert.Equal([ClockTool.Clock], plan.Slots[0].Tools);
        Assert.True(plan.Slots[1].Scrolling);
        Assert.Equal([ClockTool.Timer, ClockTool.Stopwatch, ClockTool.Alarm], plan.Slots[1].Tools);
    }

    [Fact]
    public void WithRoomForOneSlot_ItAbsorbsEveryTool_SoNothingIsUnreachable()
    {
        var config = new ClockSlotsConfig([
            ClockSlotConfig.Pinned(ClockTool.Clock),
            ClockSlotConfig.Pinned(ClockTool.Timer),
            Scroll(ClockTool.Stopwatch, ClockTool.Alarm)]);

        var plan = ClockSlotLayout.Plan(config, 150, 150);
        var only = Assert.Single(plan.Slots);
        Assert.True(only.Scrolling);
        Assert.Equal([ClockTool.Clock, ClockTool.Timer, ClockTool.Stopwatch, ClockTool.Alarm], only.Tools);
    }

    [Fact]
    public void Absorbing_NeverDuplicatesATool_AndOnlyKeepsEmptyIfTheSlotHadIt()
    {
        var config = new ClockSlotsConfig([
            Scroll(ClockTool.Timer, ClockTool.Empty),
            Scroll(ClockTool.Timer, ClockTool.Alarm, ClockTool.Empty)]);

        var plan = ClockSlotLayout.Plan(config, 150, 150);
        Assert.Equal([ClockTool.Timer, ClockTool.Alarm, ClockTool.Empty], Assert.Single(plan.Slots).Tools);

        var noEmpty = ClockSlotLayout.Plan(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock), Scroll(ClockTool.Timer, ClockTool.Empty)]), 150, 150);
        Assert.Equal([ClockTool.Clock, ClockTool.Timer], Assert.Single(noEmpty.Slots).Tools);
    }

    [Fact]
    public void ASlotThatIsNotAbsorbingStaysPinned()
    {
        var plan = ClockSlotLayout.Plan(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock)]), 640, 300);
        Assert.False(plan.Slots[0].Scrolling);
    }

    [Theory]
    [InlineData(0, 4, 1, 1)]
    [InlineData(0, 4, -1, 3)]
    [InlineData(3, 4, 1, 0)]
    [InlineData(2, 4, 0, 2)]
    [InlineData(0, 1, 1, 0)]
    [InlineData(0, 0, 1, 0)]
    public void Cycling_WrapsAtBothEnds(int index, int count, int step, int expected) =>
        Assert.Equal(expected, ClockSlotLayout.Cycle(index, count, step));

    [Theory]
    [InlineData(120, -1)] // wheel up: back
    [InlineData(-120, 1)] // wheel down: forward
    [InlineData(0, 0)]
    public void TheWheelDelta_GivesOneStepPerNotch(double delta, int step) => Assert.Equal(step, ClockSlotLayout.StepOf(delta));
}
