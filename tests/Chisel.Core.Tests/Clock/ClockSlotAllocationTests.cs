using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

// How the clock widget divides its room: every slot gets an equal cell, with the same margin at both ends and the same gap
// between cells, so one, two or three items sit centered and evenly spaced at any size.
public class ClockSlotAllocationTests
{
    private static ClockSlotsConfig Items(int count)
    {
        var all = new[] { ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Timer), ClockSlotConfig.Pinned(ClockTool.Alarm) };
        return new ClockSlotsConfig(all.Take(count).ToList());
    }

    private static double Total(ClockLayoutPlan plan) =>
        plan.Margin * 2 + plan.Gap * (plan.Slots.Count - 1) + plan.Slots.Sum(s => s.Extent);

    [Theory]
    [InlineData(1, 640, 300)]
    [InlineData(2, 640, 300)]
    [InlineData(3, 960, 300)]
    [InlineData(3, 1920, 400)]
    [InlineData(2, 300, 640)] // a tall widget stacks them
    [InlineData(3, 300, 960)]
    public void TheCellsAreEqual_AndMarginsGapsAndCellsFillTheRoomExactly(int count, double width, double height)
    {
        var plan = ClockSlotLayout.Plan(Items(count), width, height);

        Assert.Equal(count, plan.Slots.Count);
        Assert.All(plan.Slots, s => Assert.Equal(plan.Slots[0].Extent, s.Extent, 6));
        Assert.Equal(plan.Horizontal ? width : height, Total(plan), 6);
        Assert.Equal(ClockSlotLayout.OuterMargin, plan.Margin);
        Assert.Equal(ClockSlotLayout.SlotGap, plan.Gap);
    }

    [Fact]
    public void OneSlot_SpansTheWholeRoomLessTheMargins_SoTheContentIsCentered()
    {
        var plan = ClockSlotLayout.Plan(Items(1), 640, 300);

        Assert.Equal([640d - 2 * ClockSlotLayout.OuterMargin], plan.Slots.Select(s => s.Extent).ToArray());
    }

    [Fact]
    public void TheSameConfig_GivesTheSameGapsWhetherTheItemsAreClocksOrTools()
    {
        var clocks = new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Clock)]);

        var mixed = ClockSlotLayout.Plan(Items(3), 960, 300);
        var same = ClockSlotLayout.Plan(clocks, 960, 300);

        Assert.Equal(mixed.Slots.Select(s => s.Extent), same.Slots.Select(s => s.Extent));
    }

    [Theory]
    [InlineData(150, 150, 1)]
    [InlineData(330, 300, 1)]
    [InlineData(400, 300, 2)]
    [InlineData(640, 300, 3)]
    [InlineData(1920, 300, 3)]
    [InlineData(200, 700, 3)] // a tall one counts height
    public void HowManyFit_NeedsTheMinimumCellsPlusMarginsAndGaps(double width, double height, int expected)
    {
        Assert.Equal(expected, ClockSlotLayout.Capacity(width, height));
        var plan = ClockSlotLayout.Plan(Items(3), width, height);
        Assert.Equal(expected, plan.Slots.Count);
        Assert.All(plan.Slots, s => Assert.True(s.Extent >= ClockSlotLayout.MinSlotExtent - 0.001 || plan.Slots.Count == 1));
    }

    [Fact]
    public void TheAbsorbedSlotRuleStillHolds_AndTheCellsStillFillTheRoom()
    {
        var config = new ClockSlotsConfig([
            ClockSlotConfig.Pinned(ClockTool.Clock),
            ClockSlotConfig.Pinned(ClockTool.Timer),
            ClockSlotConfig.Scrolling(ClockTool.Stopwatch, ClockTool.Alarm)]);

        var plan = ClockSlotLayout.Plan(config, 400, 300); // room for two
        Assert.Equal(2, plan.Slots.Count);
        Assert.Equal([ClockTool.Timer, ClockTool.Stopwatch, ClockTool.Alarm], plan.Slots[1].Tools);
        Assert.Equal(400, Total(plan), 6);
    }

    [Theory]
    [InlineData(150, 150)]
    [InlineData(200, 700)]
    [InlineData(333, 211)]
    [InlineData(1920, 300)]
    public void TheRoomIsAlwaysFullyAccountedFor(double width, double height)
    {
        var plan = ClockSlotLayout.Plan(Items(3), width, height);

        Assert.Equal(plan.Horizontal ? width : height, Total(plan), 6);
    }
}
