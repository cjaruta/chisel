using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

public class ClockFaceLibraryTests
{
    private static readonly ClockStyle Look = ClockPresets.All[1].Style with { Alignment = ClockAlignment.Left, ShowSeconds = false };

    private static readonly ClockSlotsConfig Layout = new([ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Stopwatch)]);

    [Fact]
    public void SaveAsNew_KeepsTheStyleAndLayout_TrimsTheName_AndStartsEmpty()
    {
        var library = new ClockFaceLibrary();
        Assert.Empty(library.Faces);

        var result = library.SaveAsNew("  My face  ", Look, Layout);

        Assert.True(result.IsOk, result.Error);
        var face = Assert.Single(library.Faces);
        Assert.Equal("My face", face.Name);
        Assert.Equal(Look, face.Style);
        Assert.Equal(Layout, face.Slots);
        Assert.Equal(face, result.Face);
        Assert.NotEqual(Guid.Empty, face.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameIsRefused(string name)
    {
        var library = new ClockFaceLibrary();

        var result = library.SaveAsNew(name, Look, Layout);

        Assert.False(result.IsOk);
        Assert.Contains("name", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(library.Faces);
    }

    [Fact]
    public void NamesMustBeUnique_IgnoringCase_AndCannotBeABuiltInFacesName()
    {
        var library = new ClockFaceLibrary();
        library.SaveAsNew("Evening", Look, Layout);

        Assert.False(library.SaveAsNew("evening", Look, Layout).IsOk);
        Assert.False(library.SaveAsNew("Minimal", Look, Layout).IsOk);
        Assert.False(library.SaveAsNew("ROMAN", Look, Layout).IsOk);
        Assert.Single(library.Faces);
    }

    [Fact]
    public void ANameThatIsTooLong_IsRefused()
    {
        var library = new ClockFaceLibrary();

        Assert.False(library.SaveAsNew(new string('x', ClockFaceLibrary.MaxNameLength + 1), Look, Layout).IsOk);
        Assert.True(library.SaveAsNew(new string('x', ClockFaceLibrary.MaxNameLength), Look, Layout).IsOk);
    }

    [Fact]
    public void Rename_ChangesOnlyTheName_AndAllowsKeepingItsOwnName()
    {
        var library = new ClockFaceLibrary();
        var id = library.SaveAsNew("One", Look, Layout).Face!.Id;
        library.SaveAsNew("Two", Look, Layout);

        Assert.True(library.Rename(id, "One").IsOk); // its own name again is fine
        Assert.False(library.Rename(id, "Two").IsOk);
        Assert.True(library.Rename(id, "Uno").IsOk);

        var face = library.Find(id)!;
        Assert.Equal("Uno", face.Name);
        Assert.Equal(Look, face.Style);
        Assert.Equal(new[] { "Uno", "Two" }, library.Faces.Select(f => f.Name).ToArray());
    }

    [Fact]
    public void Update_ReplacesTheStyleAndLayout_KeepingTheNameAndPlace()
    {
        var library = new ClockFaceLibrary();
        library.SaveAsNew("First", Look, Layout);
        var id = library.SaveAsNew("Second", Look, Layout).Face!.Id;
        var newLook = ClockPresets.All[2].Style;
        var newLayout = new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock)]);

        Assert.True(library.Update(id, newLook, newLayout).IsOk);

        var face = library.Find(id)!;
        Assert.Equal("Second", face.Name);
        Assert.Equal(newLook, face.Style);
        Assert.Equal(newLayout, face.Slots);
        Assert.Equal(new[] { "First", "Second" }, library.Faces.Select(f => f.Name).ToArray());
    }

    [Fact]
    public void Delete_RemovesIt_AndAnUnknownIdIsAnError()
    {
        var library = new ClockFaceLibrary();
        var id = library.SaveAsNew("Gone", Look, Layout).Face!.Id;

        Assert.True(library.Delete(id).IsOk);
        Assert.Empty(library.Faces);
        Assert.False(library.Delete(id).IsOk);
        Assert.False(library.Rename(id, "X").IsOk);
        Assert.False(library.Update(id, Look, Layout).IsOk);
    }

    [Fact]
    public void Duplicate_MakesACopyWithAFreshIdAndAUniqueName()
    {
        var library = new ClockFaceLibrary();
        var id = library.SaveAsNew("Night", Look, Layout).Face!.Id;

        var first = library.Duplicate(id);
        var second = library.Duplicate(id);

        Assert.True(first.IsOk && second.IsOk);
        Assert.Equal(new[] { "Night", "Night copy", "Night copy 2" }, library.Faces.Select(f => f.Name).ToArray());
        Assert.Equal(3, library.Faces.Select(f => f.Id).Distinct().Count());
        Assert.Equal(Look, first.Face!.Style);
    }

    [Fact]
    public void TheLibraryHoldsAtMostAFixedNumberOfFaces()
    {
        var library = new ClockFaceLibrary();
        for (var i = 0; i < ClockFaceLibrary.MaxFaces; i++)
        {
            Assert.True(library.SaveAsNew($"Face {i}", Look, Layout).IsOk);
        }

        Assert.False(library.SaveAsNew("One too many", Look, Layout).IsOk);
    }

    [Fact]
    public void SavedStylesAreNormalized()
    {
        var library = new ClockFaceLibrary();

        var face = library.SaveAsNew("Wild", Look with { BorderWidth = 99, HourThickness = -5 }, Layout).Face!;

        Assert.Equal(ClockStyle.MaxBorder, face.Style.BorderWidth);
        Assert.Equal(ClockStyle.MinThickness, face.Style.HourThickness);
    }
}
