using Chisel.App.ViewModels;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// The settings and the face picker never change a built-in face: customizing belongs to the working configuration or to a saved
// custom face, and switching away from a face and back always gives its default look.
public class ClockTemplateSettingsTests
{
    private sealed class FakeStyleRepository : IClockStyleRepository
    {
        public ClockStyle Saved { get; private set; } = ClockStyle.Default;
        public ClockStyle Get() => Saved;
        public void Save(ClockStyle style) => Saved = style;
    }

    private static readonly string[] Ids = ClockPresets.All.Select(p => p.Id).ToArray();

    private static int IndexOf(string id) => Array.IndexOf(Ids, id);

    private static void AssertIsTheTemplate(ClockSettingsViewModel settings, string id)
    {
        Assert.Equal(ClockPresets.All[IndexOf(id)].Style.WithoutPreferences(), settings.Style.WithoutPreferences());
        Assert.Equal(IndexOf(id), settings.PresetIndex);
    }

    [Fact]
    public void ChoosingFacesInEveryKindOfOrder_ShowsEachAsItsDefinition_WithNothingLeftOver()
    {
        var settings = new ClockSettingsViewModel(new FakeStyleRepository());
        var random = new Random(5);
        var orders = new List<string[]>
        {
            Ids, Ids.Reverse().ToArray(),
            Ids.SelectMany(a => new[] { a, "pilot", a }).ToArray(),
            Enumerable.Range(0, 80).Select(_ => Ids[random.Next(Ids.Length)]).ToArray(),
        };

        foreach (var order in orders)
        {
            foreach (var id in order)
            {
                settings.PresetIndex = IndexOf(id);
                AssertIsTheTemplate(settings, id);
            }
        }
    }

    [Fact]
    public void Customizing_ChangesTheWorkingStyleOnly_NeverTheTemplate_AndSwitchingBackGivesTheDefaultLook()
    {
        var templates = ClockPresets.All.Select(p => p.Style).ToList();
        var settings = new ClockSettingsViewModel(new FakeStyleRepository());
        settings.PresetIndex = IndexOf("pilot");

        settings.HourThickness = 10;
        settings.PaletteIndex = ClockPalettes.All.ToList().FindIndex(p => p.Id == "forest");
        settings.MarkerIndex = (int)MarkerStyle.Dots;
        settings.Tachymeter = true;
        settings.KindIndex = (int)DialKind.TwentyFourHour;
        Assert.Equal(-1, settings.PresetIndex); // a custom look now

        Assert.Equal(templates, ClockPresets.All.Select(p => p.Style).ToList());
        settings.PresetIndex = IndexOf("slim");
        settings.PresetIndex = IndexOf("pilot"); // away and back
        AssertIsTheTemplate(settings, "pilot");
        Assert.Equal(ClockPresets.All[IndexOf("pilot")].Style.WithoutPreferences(), settings.Style.WithoutPreferences());
        Assert.Equal(6, settings.Style.HourThickness); // not the 10 that was set while customizing
    }

    [Fact]
    public void ResetToPreset_PutsTheWholeFaceBack_ColorsIncluded()
    {
        var settings = new ClockSettingsViewModel(new FakeStyleRepository());
        settings.PresetIndex = IndexOf("classic");
        settings.PaletteIndex = ClockPalettes.All.ToList().FindIndex(p => p.Id == "ocean");
        settings.HandIndex = (int)HandStyle.Baton;
        Assert.Equal(-1, settings.PresetIndex);

        settings.ResetToPresetCommand.Execute(null);

        AssertIsTheTemplate(settings, "classic");
    }

    [Fact]
    public void TheUsersPreferences_StayTheirOwn_WhicheverFaceIsChosen()
    {
        var settings = new ClockSettingsViewModel(new FakeStyleRepository());
        settings.AlignmentIndex = (int)ClockAlignment.Right;
        settings.ShowSeconds = false;
        settings.StopwatchIndex = (int)StopwatchDisplay.Analog;

        foreach (var id in Ids)
        {
            settings.PresetIndex = IndexOf(id);
            Assert.Equal(ClockAlignment.Right, settings.Style.Alignment);
            Assert.False(settings.Style.ShowSeconds);
            Assert.Equal(StopwatchDisplay.Analog, settings.Style.Stopwatch);
        }
    }

    [Fact]
    public void TheFacePicker_AppliesEachBuiltInFaceInFull_AndNeverEditsOne()
    {
        var settings = new ClockSettingsViewModel(new FakeStyleRepository());
        var tools = new ClockToolsViewModel(null, new FakeTimeProvider());
        var faces = new ClockFacesViewModel(null, settings, tools);
        var titles = faces.BuiltIn.Select(f => (f.Title, f.Style)).ToList();

        settings.PaletteIndex = ClockPalettes.All.ToList().FindIndex(p => p.Id == "amber");
        foreach (var face in faces.BuiltIn.Concat(faces.BuiltIn.Reverse()).Concat(faces.BuiltIn.Skip(3)))
        {
            faces.ApplyCommand.Execute(face);
            Assert.Equal(face.Style.WithoutPreferences(), settings.Style.WithoutPreferences());
            Assert.True(face.IsApplied);
            Assert.Single(faces.BuiltIn, f => f.IsApplied); // and only that one
        }

        Assert.Equal(titles, faces.BuiltIn.Select(f => (f.Title, f.Style)).ToList());
    }

    [Fact]
    public void ASavedCustomFace_IsNotChangedBySwitchingFaces_AndBuiltInOnesNotByCustomizingIt()
    {
        var settings = new ClockSettingsViewModel(new FakeStyleRepository());
        var tools = new ClockToolsViewModel(null, new FakeTimeProvider());
        var faces = new ClockFacesViewModel(null, settings, tools);
        settings.PresetIndex = IndexOf("chrono");
        settings.HourThickness = 8;
        faces.NewName = "My chrono";
        faces.SaveAsNewCommand.Execute(null);
        var saved = faces.Custom.Single().Style;

        settings.PresetIndex = IndexOf("slim");
        settings.PresetIndex = IndexOf("railway");
        faces.ApplyCommand.Execute(faces.Custom.Single());

        Assert.Equal(saved, faces.Custom.Single().Style);
        Assert.Equal(8, settings.Style.HourThickness);
        Assert.Equal(ClockPresets.All[IndexOf("chrono")].Style.HourThickness, 5); // the built-in chrono still has its own thickness
        faces.ApplyCommand.Execute(faces.BuiltIn.Single(f => f.Title == "Chrono"));
        Assert.Equal(5, settings.Style.HourThickness);
    }
}
