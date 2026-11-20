using Chisel.App.ViewModels;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class ClockSlotsSettingsViewModelTests
{
    private sealed class MemoryTools : IClockToolsRepository
    {
        public ClockToolsState Saved { get; private set; } = new(ClockSlotsConfig.Default, TimerState.Idle, StopwatchState.Idle);
        public ClockToolsState Get() => Saved;
        public void Save(ClockToolsState state) => Saved = state;
    }

    private static (ClockSlotsSettingsViewModel Vm, ClockToolsViewModel Tools, MemoryTools Repo) Make(ClockSlotsConfig? start = null)
    {
        var repo = new MemoryTools();
        var tools = new ClockToolsViewModel(repo, new FakeTimeProvider());
        if (start is not null)
        {
            tools.SetSlots(start);
        }
        return (new ClockSlotsSettingsViewModel(tools), tools, repo);
    }

    [Fact]
    public void ItShowsTheSavedSlots_TheDefaultBeingTheClockAndOneScrollingSlot()
    {
        var (vm, _, _) = Make();

        Assert.Equal(2, vm.SlotCount);
        Assert.Equal(1, vm.SlotCountIndex);
        Assert.False(vm.Slots[0].Scrolling);
        Assert.Equal((int)ClockTool.Clock, vm.Slots[0].PinnedIndex);
        Assert.True(vm.Slots[1].Scrolling);
        Assert.True(vm.Slots[1].HasTimer && vm.Slots[1].HasStopwatch && vm.Slots[1].HasAlarm && vm.Slots[1].HasEmpty);
        Assert.False(vm.Slots[1].HasClock);
        Assert.True(vm.Slots[0].IsShown);
        Assert.True(vm.Slots[1].IsShown);
        Assert.False(vm.Slots[2].IsShown);
    }

    [Fact]
    public void TheSlotCount_AddsAndRemovesSlots_AndSavesThem()
    {
        var (vm, tools, repo) = Make();

        vm.SlotCountIndex = 2;
        Assert.Equal(3, tools.Slots.Slots.Count);
        Assert.Equal(tools.Slots, repo.Saved.Slots);
        Assert.True(vm.Slots[2].IsShown);

        vm.SlotCountIndex = 0;
        Assert.Single(tools.Slots.Slots);
        Assert.Equal([ClockTool.Clock], tools.Slots.Slots[0].Tools);
    }

    [Fact]
    public void ASlotRemovedAndAddedAgain_KeepsItsSettings()
    {
        var (vm, tools, _) = Make();
        vm.SlotCountIndex = 0;
        vm.SlotCountIndex = 1;
        Assert.Equal(ClockSlotsConfig.Default, tools.Slots);
    }

    [Fact]
    public void SwitchingASlotToPinned_UsesTheChosenTool()
    {
        var (vm, tools, _) = Make();

        vm.Slots[1].Scrolling = false;
        vm.Slots[1].PinnedIndex = (int)ClockTool.Alarm;

        Assert.Equal(ClockSlotConfig.Pinned(ClockTool.Alarm), tools.Slots.Slots[1]);
    }

    [Fact]
    public void AScrollingSlot_ScrollsThroughTheToolsChecked()
    {
        var (vm, tools, _) = Make();

        vm.Slots[1].HasTimer = false;
        vm.Slots[1].HasEmpty = false;
        vm.Slots[1].HasClock = true;

        Assert.Equal([ClockTool.Clock, ClockTool.Stopwatch, ClockTool.Alarm], tools.Slots.Slots[1].Tools);
    }

    [Fact]
    public void AScrollingSlotWithNothingChecked_IsJustEmpty()
    {
        var (vm, tools, _) = Make(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Scrolling(ClockTool.Timer)]));

        vm.Slots[1].HasTimer = false;

        Assert.Equal([ClockTool.Empty], tools.Slots.Slots[1].Tools);
    }

    [Fact]
    public void TheMainSlotIsLabelled_AndTheOthersAreNumbered()
    {
        var (vm, _, _) = Make();
        Assert.Equal(["Main slot", "Slot 2", "Slot 3"], vm.Slots.Select(s => s.Title));
    }

    [Fact]
    public void AChangeMadeElsewhere_IsReflectedInTheControls()
    {
        var (vm, tools, _) = Make();
        tools.SetSlots(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Stopwatch)]));

        Assert.Equal(0, vm.SlotCountIndex);
        Assert.Equal((int)ClockTool.Stopwatch, vm.Slots[0].PinnedIndex);
    }
}
