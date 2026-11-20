using Chisel.App.ViewModels;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class ClockSettingsPageViewModelTests
{
    private sealed class MemoryStyles(ClockStyle? start = null) : IClockStyleRepository
    {
        public ClockStyle Saved { get; private set; } = start ?? ClockStyle.Default;
        public ClockStyle Get() => Saved;
        public void Save(ClockStyle style) => Saved = style;
    }

    private sealed class MemoryTools : IClockToolsRepository
    {
        public ClockToolsState Saved { get; private set; } = new(ClockSlotsConfig.Default, TimerState.Idle, StopwatchState.Idle);
        public ClockToolsState Get() => Saved;
        public void Save(ClockToolsState state) => Saved = state;
    }

    private static (ClockSettingsViewModel Vm, MemoryStyles Repo) Make(ClockStyle? start = null)
    {
        var repo = new MemoryStyles(start);
        return (new ClockSettingsViewModel(repo), repo);
    }

    [Fact]
    public void ThePageOpensOnTheLayoutTab_AndEachTabCanBeSelected()
    {
        var (vm, _) = Make();
        Assert.Equal(ClockSettingsTab.Layout, vm.Tab);
        Assert.True(vm.IsLayoutTab);

        foreach (var tab in Enum.GetValues<ClockSettingsTab>())
        {
            vm.SelectTabCommand.Execute(tab);
            Assert.Equal(tab, vm.Tab);
        }
        vm.SelectTabCommand.Execute(ClockSettingsTab.Tools);
        Assert.True(vm.IsToolsTab);
        Assert.False(vm.IsFaceTab || vm.IsLayoutTab || vm.IsBehaviorTab);
    }

    [Fact]
    public void TheAlignmentButtons_SelectAndShowTheCurrentOne_AndSaveIt()
    {
        var (vm, repo) = Make();
        Assert.True(vm.IsAlignmentCenter);

        vm.SelectAlignmentCommand.Execute(ClockAlignment.Right);

        Assert.True(vm.IsAlignmentRight);
        Assert.False(vm.IsAlignmentCenter || vm.IsAlignmentLeft);
        Assert.Equal(ClockAlignment.Right, repo.Saved.Alignment);
    }

    [Fact]
    public void ResetToPreset_PutsTheChosenPresetLookBack_KeepingAlignmentSecondsAndColors()
    {
        var (vm, repo) = Make(ClockStyle.Default with { Alignment = ClockAlignment.Left, ShowSeconds = false, Colors = new ClockColors("#112233", null, null, null, null, "#445566") });
        vm.PresetIndex = ClockPresets.All.ToList().FindIndex(p => p.Id == "roman");
        vm.HourThickness = 9;
        vm.HandIndex = (int)HandStyle.Baton;
        Assert.Equal(-1, vm.PresetIndex);
        Assert.True(vm.ResetToPresetCommand.CanExecute(null));

        vm.ResetToPresetCommand.Execute(null);

        Assert.Equal("roman", ClockPresets.IdOf(repo.Saved));
        Assert.Equal(NumeralStyle.Roman, repo.Saved.Numerals);
        Assert.Equal(ClockAlignment.Left, repo.Saved.Alignment);
        Assert.False(repo.Saved.ShowSeconds);
        Assert.Equal("#112233", repo.Saved.Colors.Dial);
        Assert.Equal("#445566", repo.Saved.Colors.Second);
        Assert.False(vm.ResetToPresetCommand.CanExecute(null)); // already matches
    }

    [Fact]
    public void ResetToPreset_FromACustomSavedLook_UsesTheFirstPreset()
    {
        var (vm, repo) = Make(ClockStyle.Default with { Hands = HandStyle.Leaf, HourThickness = 8, Numerals = NumeralStyle.Four, Markers = MarkerStyle.Bars });
        Assert.Equal(-1, vm.PresetIndex);

        vm.ResetToPresetCommand.Execute(null);

        Assert.Equal(ClockPresets.All[0].Id, ClockPresets.IdOf(repo.Saved));
    }

    [Fact]
    public void ResetToPreset_IsNotOfferedWhileThePresetIsExact()
    {
        var (vm, _) = Make();
        vm.PresetIndex = 2;
        Assert.False(vm.ResetToPresetCommand.CanExecute(null));
    }

    [Fact]
    public void EveryStyleOption_IsStillReachableFromTheViewModelAndPersisted()
    {
        var (vm, repo) = Make();
        vm.AlignmentIndex = (int)ClockAlignment.Left;
        vm.ShapeIndex = (int)DialShape.RoundedSquare;
        vm.BorderWidth = 5;
        vm.MarkerIndex = (int)MarkerStyle.Dots;
        vm.EmphasizeQuarters = false;
        vm.NumeralIndex = (int)NumeralStyle.Roman;
        vm.HandIndex = (int)HandStyle.Tapered;
        vm.HourThickness = 6;
        vm.MinuteThickness = 5;
        vm.SecondThickness = 2;
        vm.ShowSeconds = false;
        vm.PaletteIndex = 2;
        vm.DialColorIndex = 1;
        vm.MarkerColorIndex = 2;
        vm.NumeralColorIndex = 3;
        vm.HourColorIndex = 4;
        vm.MinuteColorIndex = 5;
        vm.SecondColorIndex = 6;

        var saved = repo.Saved;
        Assert.Equal((ClockAlignment.Left, DialShape.RoundedSquare, 5d, MarkerStyle.Dots, false, NumeralStyle.Roman, HandStyle.Tapered),
            (saved.Alignment, saved.Shape, saved.BorderWidth, saved.Markers, saved.EmphasizeQuarters, saved.Numerals, saved.Hands));
        Assert.Equal((6d, 5d, 2d, false), (saved.HourThickness, saved.MinuteThickness, saved.SecondThickness, saved.ShowSeconds));
        Assert.Equal(new ClockColors(ClockSwatches.All[1].Hex, ClockSwatches.All[2].Hex, ClockSwatches.All[3].Hex, ClockSwatches.All[4].Hex, ClockSwatches.All[5].Hex, ClockSwatches.All[6].Hex), saved.Colors);
    }

    [Fact]
    public void TheSlotCountButtons_SelectOneTwoOrThree_AndShowTheCurrent()
    {
        var tools = new ClockToolsViewModel(new MemoryTools(), new FakeTimeProvider());
        var vm = new ClockSlotsSettingsViewModel(tools);
        Assert.True(vm.IsSlotCountTwo);

        vm.SelectSlotCountCommand.Execute(3);
        Assert.True(vm.IsSlotCountThree);
        Assert.Equal(3, tools.Slots.Slots.Count);

        vm.SelectSlotCountCommand.Execute(1);
        Assert.True(vm.IsSlotCountOne);
        Assert.False(vm.IsSlotCountTwo || vm.IsSlotCountThree);
        Assert.Single(tools.Slots.Slots);
    }

    [Fact]
    public void ASlotsModeButtons_SwitchBetweenPinnedAndScrolling()
    {
        var tools = new ClockToolsViewModel(new MemoryTools(), new FakeTimeProvider());
        var vm = new ClockSlotsSettingsViewModel(tools);

        vm.Slots[1].SelectModeCommand.Execute(false);
        Assert.False(vm.Slots[1].Scrolling);
        Assert.True(vm.Slots[1].IsPinned);
        Assert.False(tools.Slots.Slots[1].IsScrolling);

        vm.Slots[1].SelectModeCommand.Execute(true);
        Assert.True(tools.Slots.Slots[1].IsScrolling);
    }
}
