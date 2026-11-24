using Chisel.Core.Theme;

namespace Chisel.Core.Tests.Theme;

public class ThemeModesTests
{
    public static IEnumerable<object[]> Modes => ThemeModes.All.Select(m => new object[] { m.Id });

    public static IEnumerable<object[]> ModesAndAccents =>
        from mode in ThemeModes.All
        from accent in new[] { ThemeModes.DefaultAccent, "#FFFFFF", "#000000", "#0A1A5C", "#FFE600" }
        select new object[] { mode.Id, accent };

    [Fact]
    public void HasDefaultLightUltraDarkAndFourSchemes()
    {
        Assert.Equal(new[] { "default", "light", "mist", "sage", "ultra-dark", "ocean", "forest", "plum", "slate", "midnight", "ember" }, ThemeModes.All.Select(m => m.Id));
        Assert.Equal(new[] { "Default", "Light", "Mist", "Sage", "Ultra dark", "Ocean", "Forest", "Plum", "Slate", "Midnight", "Ember" }, ThemeModes.All.Select(m => m.Name));
    }

    [Fact]
    public void DefaultMode_KeepsTheExistingLook()
    {
        Assert.Equal("#14161A", ThemeModes.Default.Window.ToHex());
        Assert.Equal("#1F222A", ThemeModes.Default.Card.ToHex());
        Assert.Equal("#F5A524", ThemeModes.DefaultAccent);
    }

    [Fact]
    public void AccentPresets_StartWithTheDefaultAndAreValidAndDistinct()
    {
        Assert.Equal(ThemeModes.DefaultAccent, ThemeModes.AccentPresets[0].Hex);
        Assert.All(ThemeModes.AccentPresets, p => Assert.True(Rgb.TryParse(p.Hex, out _)));
        Assert.Equal(ThemeModes.AccentPresets.Count, ThemeModes.AccentPresets.Select(p => p.Hex).Distinct().Count());
    }

    [Fact]
    public void UltraDark_UsesTrueBlackWindow() => Assert.Equal("#000000", ThemeModes.ById("ultra-dark").Window.ToHex());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nope")]
    public void ById_FallsBackToDefault(string? id) => Assert.Same(ThemeModes.Default, ThemeModes.ById(id));

    [Fact]
    public void ResolveAccent_FallsBackToTheDefaultOrange()
    {
        Assert.Equal(Rgb.Parse("#F5A524"), ThemeModes.ResolveAccent(null));
        Assert.Equal(Rgb.Parse("#F5A524"), ThemeModes.ResolveAccent("garbage"));
        Assert.Equal(Rgb.Parse("#112233"), ThemeModes.ResolveAccent("#112233"));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Text_AndBoundaries_AreReadableOnEverySurface(string id)
    {
        var m = ThemeModes.ById(id);
        foreach (var surface in new[] { m.Window, m.Card, m.Raised })
        {
            Assert.True(Rgb.Contrast(m.Text, surface) >= 4.5, $"{id} text {surface.ToHex()}");
            Assert.True(Rgb.Contrast(m.TextSecondary, surface) >= 3, $"{id} secondary {surface.ToHex()}");
            Assert.True(Rgb.Contrast(m.ControlBorder, surface) >= 3, $"{id} control border {surface.ToHex()}");
        }
    }

    [Theory]
    [MemberData(nameof(ModesAndAccents))]
    public void Accent_StaysReadableInEveryMode(string id, string accent)
    {
        var m = ThemeModes.ById(id);
        var t = ThemeModes.Accent(m, Rgb.Parse(accent));
        foreach (var surface in new[] { m.Window, m.Card, m.Raised })
        {
            Assert.True(Rgb.Contrast(t.Text, surface) >= 4.5, $"{id} {accent} accent text {surface.ToHex()}");
            Assert.True(Rgb.Contrast(t.Fill, surface) >= 3, $"{id} {accent} accent fill {surface.ToHex()}");
        }
        Assert.True(Rgb.Contrast(t.OnAccent, t.Fill) >= 4.5, $"{id} {accent} on-accent");
    }
}
