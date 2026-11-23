using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class ClockFacesViewModelTests
{
    private sealed class FakeStyleRepository : IClockStyleRepository
    {
        public ClockStyle Saved { get; private set; } = ClockStyle.Default;
        public ClockStyle Get() => Saved;
        public void Save(ClockStyle style) => Saved = style;
    }

    private sealed class Rig
    {
        public MemoryClockFaceStore Store { get; } = new();
        public ClockSettingsViewModel Settings { get; } = new(new FakeStyleRepository());
        public ClockToolsViewModel Tools { get; } = new(null, new FakeTimeProvider());
        public ClockFacesViewModel Faces => _faces ??= new ClockFacesViewModel(Store, Settings, Tools);
        private ClockFacesViewModel? _faces;

        public ClockFacesViewModel Reopen() => new(Store, Settings, Tools);
    }

    private static string[] Titles(IEnumerable<ClockFaceItem> items) => items.Select(i => i.Title).ToArray();

    [Fact]
    public void TheBuiltInFacesComeFirst_ThenTheSavedOnes_AndOnlyTheSavedOnesAreEditable()
    {
        var rig = new Rig();
        rig.Store.Save([new CustomClockFace(Guid.NewGuid(), "Mine", ClockPresets.All[1].Style, ClockSlotsConfig.Default)]);

        var faces = rig.Reopen();

        Assert.Equal(ClockPresets.All.Select(p => p.Title).ToArray(), Titles(faces.BuiltIn));
        Assert.Equal(new[] { "Mine" }, Titles(faces.Custom));
        Assert.All(faces.BuiltIn, f => Assert.True(f.IsBuiltIn));
        Assert.All(faces.Custom, f => Assert.False(f.IsBuiltIn));
    }

    [Fact]
    public void SaveAsNew_KeepsTheCurrentLookAndLayout_SelectsIt_AndClearsTheNameBox()
    {
        var rig = new Rig();
        rig.Settings.PresetIndex = 1;
        rig.Settings.AlignmentIndex = (int)ClockAlignment.Left;
        rig.Tools.SetSlots(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Stopwatch)]));
        var faces = rig.Faces;

        faces.NewName = "  Evening  ";
        faces.SaveAsNewCommand.Execute(null);

        var saved = Assert.Single(faces.Custom);
        Assert.Equal("Evening", saved.Title);
        Assert.Equal(rig.Settings.Style, saved.Style);
        Assert.Equal(rig.Tools.Slots, saved.Slots);
        Assert.Same(saved, faces.Selected);
        Assert.Equal(string.Empty, faces.NewName);
        Assert.Null(faces.Error);
    }

    [Fact]
    public void SaveAsNew_RefusesABlankOrTakenName_AndSaysWhy()
    {
        var rig = new Rig();
        var faces = rig.Faces;

        faces.NewName = "   ";
        faces.SaveAsNewCommand.Execute(null);
        Assert.Contains("name", faces.Error, StringComparison.OrdinalIgnoreCase);

        faces.NewName = "Minimal"; // a built-in face's name
        faces.SaveAsNewCommand.Execute(null);
        Assert.Contains("built-in", faces.Error);

        faces.NewName = "Mine";
        faces.SaveAsNewCommand.Execute(null);
        faces.NewName = "mine";
        faces.SaveAsNewCommand.Execute(null);
        Assert.Contains("already", faces.Error);
        Assert.Single(faces.Custom);
    }

    [Fact]
    public void Apply_OfASavedFace_SetsTheStyleAndTheLayout()
    {
        var rig = new Rig();
        var look = ClockPresets.All[2].Style with { Alignment = ClockAlignment.Right, ShowSeconds = false, Colors = new ClockColors("#112233", null, null, null, null, null) };
        var layout = new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Timer)]);
        rig.Store.Save([new CustomClockFace(Guid.NewGuid(), "Trio", look, layout)]);
        var faces = rig.Reopen();

        faces.ApplyCommand.Execute(faces.Custom[0]);

        Assert.Equal(look, rig.Settings.Style);
        Assert.Equal(layout, rig.Tools.Slots);
        Assert.Same(faces.Custom[0], faces.Selected);
        Assert.True(faces.Custom[0].IsApplied);
    }

    [Fact]
    public void Apply_OfABuiltInFace_UsesItsLook_KeepingTheLayoutAndTheUsersOwnChoices()
    {
        var rig = new Rig();
        rig.Settings.AlignmentIndex = (int)ClockAlignment.Right;
        var layout = rig.Tools.Slots;
        var faces = rig.Faces;
        var roman = faces.BuiltIn.Single(f => f.Title == "Roman");

        faces.ApplyCommand.Execute(roman);

        Assert.Equal(NumeralStyle.Roman, rig.Settings.Style.Numerals);
        Assert.Equal(ClockAlignment.Right, rig.Settings.Style.Alignment);
        Assert.Equal(layout, rig.Tools.Slots);
        Assert.True(roman.IsApplied);
    }

    [Fact]
    public void Update_OverwritesTheSelectedSavedFaceWithTheCurrentLook()
    {
        var rig = new Rig();
        var faces = rig.Faces;
        faces.NewName = "Mine";
        faces.SaveAsNewCommand.Execute(null);
        rig.Settings.PresetIndex = 3;

        faces.UpdateSelectedCommand.Execute(null);

        Assert.Equal(rig.Settings.Style, faces.Custom[0].Style);
        Assert.Equal("Mine", faces.Custom[0].Title);
    }

    [Fact]
    public void Rename_ChangesTheSelectedSavedFaceOnly()
    {
        var rig = new Rig();
        var faces = rig.Faces;
        faces.NewName = "Old";
        faces.SaveAsNewCommand.Execute(null);

        faces.NewName = "Fresh";
        faces.RenameSelectedCommand.Execute(null);

        Assert.Equal(new[] { "Fresh" }, Titles(faces.Custom));
        Assert.Equal("Fresh", faces.Selected!.Title);
        Assert.Equal(string.Empty, faces.NewName);
    }

    [Fact]
    public void Delete_RemovesTheSelectedSavedFace()
    {
        var rig = new Rig();
        var faces = rig.Faces;
        faces.NewName = "Gone";
        faces.SaveAsNewCommand.Execute(null);

        faces.DeleteSelectedCommand.Execute(null);

        Assert.Empty(faces.Custom);
        Assert.Null(faces.Selected);
        Assert.Empty(rig.Store.Load());
    }

    [Fact]
    public void Duplicate_MakesACopyOfASavedFace_AndOfABuiltInOne()
    {
        var rig = new Rig();
        var faces = rig.Faces;
        faces.NewName = "Night";
        faces.SaveAsNewCommand.Execute(null);

        faces.DuplicateSelectedCommand.Execute(null);
        Assert.Equal(new[] { "Night", "Night copy" }, Titles(faces.Custom));

        faces.Selected = faces.BuiltIn.Single(f => f.Title == "Dots");
        faces.DuplicateSelectedCommand.Execute(null);
        Assert.Contains("Dots copy", Titles(faces.Custom));
        Assert.Equal(ClockPresets.All.Single(p => p.Title == "Dots").Style.Markers, faces.Custom.Single(f => f.Title == "Dots copy").Style.Markers);
    }

    [Fact]
    public void BuiltInFaces_CannotBeRenamedUpdatedOrDeleted()
    {
        var rig = new Rig();
        var faces = rig.Faces;
        faces.Selected = faces.BuiltIn[0];
        var before = ClockPresets.All[0];

        Assert.False(faces.RenameSelectedCommand.CanExecute(null));
        Assert.False(faces.UpdateSelectedCommand.CanExecute(null));
        Assert.False(faces.DeleteSelectedCommand.CanExecute(null));
        Assert.Equal(before.Title, faces.BuiltIn[0].Title);
        Assert.Equal(ClockPresets.All.Count, faces.BuiltIn.Count);
    }

    [Fact]
    public void EveryChange_IsSavedAtOnce_AndSurvivesReopening()
    {
        var rig = new Rig();
        var faces = rig.Faces;
        faces.NewName = "One";
        faces.SaveAsNewCommand.Execute(null);
        faces.NewName = "Two";
        faces.SaveAsNewCommand.Execute(null);
        faces.Selected = faces.Custom[0];
        faces.NewName = "Uno";
        faces.RenameSelectedCommand.Execute(null);

        var reopened = rig.Reopen();

        Assert.Equal(new[] { "Uno", "Two" }, Titles(reopened.Custom));
        Assert.Equal(faces.Custom[1].Style, reopened.Custom[1].Style);
        Assert.Equal(faces.Custom[1].Slots, reopened.Custom[1].Slots);
    }

    [Fact]
    public void CorruptStoredText_ShowsNoSavedFaces_AndTheBuiltInOnesStillWork()
    {
        var rig = new Rig();
        rig.Store.SetRawText("{ this is not a face file");

        var faces = rig.Reopen();

        Assert.Empty(faces.Custom);
        Assert.Equal(ClockPresets.All.Count, faces.BuiltIn.Count);
        faces.NewName = "Fresh";
        faces.SaveAsNewCommand.Execute(null);
        Assert.Equal(new[] { "Fresh" }, Titles(rig.Reopen().Custom));
    }

    [Fact]
    public void ExportAndImport_MoveAFaceThroughText()
    {
        var rig = new Rig();
        var faces = rig.Faces;
        faces.NewName = "Share";
        faces.SaveAsNewCommand.Execute(null);
        var text = faces.Export(faces.Custom[0]);

        var other = new Rig().Faces;
        Assert.True(other.Import(text));

        var imported = Assert.Single(other.Custom);
        Assert.Equal("Share", imported.Title);
        Assert.Equal(faces.Custom[0].Style, imported.Style);
        Assert.False(other.Import("garbage"));
        Assert.Contains("face", other.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(other.Import(text)); // a second copy gets its own name
        Assert.Equal(new[] { "Share", "Share copy" }, Titles(other.Custom));
    }
}
