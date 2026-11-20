using Chisel.App.ViewModels;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.ViewModels;

public class ClockSettingsViewModelTests
{
    private sealed class MemoryStyles(ClockStyle? start = null) : IClockStyleRepository
    {
        public ClockStyle Saved { get; private set; } = start ?? ClockStyle.Default;
        public int Saves { get; private set; }
        public ClockStyle Get() => Saved;
        public void Save(ClockStyle style) { Saved = style; Saves++; }
    }

    [Fact]
    public void LoadsTheSavedStyle_AndShowsItsOptions()
    {
        var saved = ClockStyle.Default with { Alignment = ClockAlignment.Right, Numerals = NumeralStyle.Roman, Hands = HandStyle.Leaf, HourThickness = 7 };
        var vm = new ClockSettingsViewModel(new MemoryStyles(saved));

        Assert.Equal(saved, vm.Style);
        Assert.Equal((int)ClockAlignment.Right, vm.AlignmentIndex);
        Assert.Equal((int)NumeralStyle.Roman, vm.NumeralIndex);
        Assert.Equal((int)HandStyle.Leaf, vm.HandIndex);
        Assert.Equal(7, vm.HourThickness);
    }

    [Fact]
    public void ChangingAnOption_UpdatesTheStyleAndSavesIt()
    {
        var repo = new MemoryStyles();
        var vm = new ClockSettingsViewModel(repo);

        vm.AlignmentIndex = (int)ClockAlignment.Left;
        vm.ShapeIndex = (int)DialShape.Square;
        vm.MarkerIndex = (int)MarkerStyle.Dots;
        vm.ShowSeconds = false;

        Assert.Equal(ClockAlignment.Left, repo.Saved.Alignment);
        Assert.Equal(DialShape.Square, repo.Saved.Shape);
        Assert.Equal(MarkerStyle.Dots, repo.Saved.Markers);
        Assert.False(repo.Saved.ShowSeconds);
        Assert.Equal(repo.Saved, vm.Style);
    }

    [Fact]
    public void TheThickness_IsClampedToTheAllowedRange()
    {
        var vm = new ClockSettingsViewModel(new MemoryStyles());
        vm.HourThickness = 50;
        Assert.Equal(ClockStyle.MaxThickness, vm.HourThickness);
        Assert.Equal(ClockStyle.MaxThickness, vm.Style.HourThickness);
    }

    [Fact]
    public void ChoosingAPreset_AppliesItsLook_KeepsTheAlignment_AndUpdatesTheOptions()
    {
        var repo = new MemoryStyles(ClockStyle.Default with { Alignment = ClockAlignment.Left });
        var vm = new ClockSettingsViewModel(repo);

        vm.PresetIndex = ClockPresets.All.ToList().FindIndex(p => p.Id == "roman");

        Assert.Equal(NumeralStyle.Roman, repo.Saved.Numerals);
        Assert.Equal(ClockAlignment.Left, repo.Saved.Alignment);
        Assert.Equal((int)NumeralStyle.Roman, vm.NumeralIndex);
        Assert.Equal((int)HandStyle.Leaf, vm.HandIndex);
    }

    [Fact]
    public void ChangingAnOptionAfterAPreset_MakesTheLookCustom()
    {
        var vm = new ClockSettingsViewModel(new MemoryStyles());
        vm.PresetIndex = 0;
        Assert.Equal(0, vm.PresetIndex);

        vm.HourThickness += 1;

        Assert.Equal(-1, vm.PresetIndex);
    }

    [Fact]
    public void ChoosingAPalette_ColorsEveryPart_AndAPartColorCanBeChangedAlone()
    {
        var repo = new MemoryStyles();
        var vm = new ClockSettingsViewModel(repo);

        vm.PaletteIndex = ClockPalettes.All.ToList().FindIndex(p => p.Id == "ocean");
        var ocean = ClockPalettes.All.First(p => p.Id == "ocean").Colors;
        Assert.Equal(ocean, repo.Saved.Colors);

        vm.SecondColorIndex = 4; // Orange
        Assert.Equal("#E8702A", repo.Saved.Colors.Second);
        Assert.Equal(ocean.Hour, repo.Saved.Colors.Hour);
        Assert.Equal(-1, vm.PaletteIndex);
    }

    [Fact]
    public void ThePresetPicker_DoesNotSaveWhenLoading()
    {
        var repo = new MemoryStyles();
        _ = new ClockSettingsViewModel(repo);
        Assert.Equal(0, repo.Saves);
    }
}
