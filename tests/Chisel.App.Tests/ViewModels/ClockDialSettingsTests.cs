using Chisel.App.ViewModels;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.ViewModels;

// Choosing the new dials and the dial options on the Clock page, and what stays the user's own when a face is chosen.
public class ClockDialSettingsTests
{
    private sealed class FakeStyleRepository : IClockStyleRepository
    {
        public ClockStyle Saved { get; private set; } = ClockStyle.Default;
        public int Saves { get; private set; }
        public ClockStyle Get() => Saved;
        public void Save(ClockStyle style)
        {
            Saved = style;
            Saves++;
        }
    }

    private static int IndexOf(string id) => ClockPresets.All.ToList().FindIndex(p => p.Id == id);

    [Fact]
    public void EveryBuiltInFace_CanBeChosen_AndIsSavedAndRecognizedAgain()
    {
        foreach (var preset in ClockPresets.All)
        {
            var repository = new FakeStyleRepository();
            var settings = new ClockSettingsViewModel(repository);

            settings.PresetIndex = IndexOf(preset.Id);

            Assert.Equal(preset.Style.Kind, settings.Style.Kind);
            Assert.Equal(preset.Style.Markers, settings.Style.Markers);
            Assert.Equal(preset.Style.DialFill, settings.Style.DialFill);
            Assert.Equal(repository.Saved, settings.Style);
            Assert.Equal(IndexOf(preset.Id), new ClockSettingsViewModel(repository).PresetIndex);
        }
    }

    [Fact]
    public void ChoosingAFilledFace_BringsItsColors_AndAThemeFace_KeepsTheUsersColors()
    {
        var settings = new ClockSettingsViewModel(new FakeStyleRepository());
        settings.PaletteIndex = ClockPalettes.All.ToList().FindIndex(p => p.Id == "amber");
        var amber = settings.Style.Colors;

        settings.PresetIndex = IndexOf("railway");
        Assert.NotEqual(amber, settings.Style.Colors);
        Assert.Equal("#C62828", settings.Style.Colors.Second);

        settings.PresetIndex = IndexOf("slim");
        Assert.Equal("#C62828", settings.Style.Colors.Second); // a theme face keeps the colors as they are
    }

    [Fact]
    public void TheStopwatchDisplay_IsSavedAndSurvivesChoosingAFace()
    {
        var repository = new FakeStyleRepository();
        var settings = new ClockSettingsViewModel(repository);
        Assert.Equal(StopwatchDisplay.Digital, settings.Style.Stopwatch);

        settings.StopwatchIndex = (int)StopwatchDisplay.Analog;
        settings.PresetIndex = IndexOf("pilot");

        Assert.Equal(StopwatchDisplay.Analog, settings.Style.Stopwatch);
        Assert.Equal(StopwatchDisplay.Analog, new ClockSettingsViewModel(repository).Style.Stopwatch);
    }

    [Fact]
    public void TheDialOptions_ChangeTheStyle_AndMakeTheLookCustom()
    {
        var settings = new ClockSettingsViewModel(new FakeStyleRepository());
        settings.PresetIndex = IndexOf("classic");
        Assert.Equal(IndexOf("classic"), settings.PresetIndex);

        settings.KindIndex = (int)DialKind.TwentyFourHour;
        Assert.Equal(DialKind.TwentyFourHour, settings.Style.Kind);
        Assert.Equal(-1, settings.PresetIndex);

        settings.MinuteTrack = true;
        settings.Tachymeter = true;
        Assert.True(settings.Style.MinuteTrack && settings.Style.Tachymeter);
    }

    [Fact]
    public void TheNewMarkerAndHandChoices_AreListed()
    {
        Assert.Equal("Luminous", ClockSettingsViewModel.MarkerNames[(int)MarkerStyle.Lume]);
        Assert.Equal("Sword", ClockSettingsViewModel.HandNames[(int)HandStyle.Sword]);
        Assert.Equal(new[] { "Standard", "24-hour", "Chronograph" }, ClockSettingsViewModel.KindNames);
        Assert.Equal(new[] { "Digital", "Analog" }, ClockSettingsViewModel.StopwatchNames);
    }

    [Fact]
    public void ResetToPreset_PutsAFacesLookBack_AfterTheOptionsWereChanged()
    {
        var settings = new ClockSettingsViewModel(new FakeStyleRepository());
        settings.PresetIndex = IndexOf("chrono");
        settings.Tachymeter = false;
        Assert.Equal(-1, settings.PresetIndex);

        settings.ResetToPresetCommand.Execute(null);

        Assert.True(settings.Style.Tachymeter);
        Assert.Equal(IndexOf("chrono"), settings.PresetIndex);
    }
}
