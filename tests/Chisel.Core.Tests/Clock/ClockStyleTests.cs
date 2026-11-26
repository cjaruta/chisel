using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

public class ClockStyleTests
{
    [Fact]
    public void Default_IsCenteredWithASecondsHand()
    {
        var style = ClockStyle.Default;
        Assert.Equal(ClockAlignment.Center, style.Alignment);
        Assert.True(style.ShowSeconds);
    }

    [Fact]
    public void Presets_AreTheTwelveNamedLooks_WithUniqueIds()
    {
        var ids = ClockPresets.All.Select(p => p.Id).ToList();
        Assert.Equal(["minimal", "roman", "numbers", "ticks", "modern", "dots", "classic", "slim", "chrono", "hours24", "railway", "pilot"], ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Preset_KeepsTheChosenAlignment()
    {
        var style = ClockStyle.Default with { Alignment = ClockAlignment.Left };
        var applied = ClockPresets.Apply("roman", style);
        Assert.Equal(ClockAlignment.Left, applied.Alignment);
        Assert.Equal(NumeralStyle.Roman, applied.Numerals);
    }

    [Fact]
    public void Preset_WithAnUnknownId_ChangesNothing()
    {
        Assert.Equal(ClockStyle.Default, ClockPresets.Apply("nope", ClockStyle.Default));
    }

    [Fact]
    public void PresetIdOf_FindsAnExactMatch_AndOtherwiseNull()
    {
        var modern = ClockPresets.Apply("modern", ClockStyle.Default);
        Assert.Equal("modern", ClockPresets.IdOf(modern));
        Assert.Null(ClockPresets.IdOf(modern with { HourThickness = modern.HourThickness + 1 }));
    }

    [Fact]
    public void Normalized_ClampsThicknessAndDropsBadColors()
    {
        var style = ClockStyle.Default with { HourThickness = 99, MinuteThickness = 0, BorderWidth = -3, MarkerColor = "nonsense", DialColor = "#112233" };
        var fixedStyle = style.Normalized();
        Assert.Equal(ClockStyle.MaxThickness, fixedStyle.HourThickness);
        Assert.Equal(ClockStyle.MinThickness, fixedStyle.MinuteThickness);
        Assert.Equal(0, fixedStyle.BorderWidth);
        Assert.Null(fixedStyle.MarkerColor);
        Assert.Equal("#112233", fixedStyle.DialColor);
    }

    [Fact]
    public void Palettes_CoverEveryPartAndOneIsTheThemeDefault()
    {
        Assert.Contains(ClockPalettes.All, p => p.Id == "theme" && p.Colors == ClockColors.Theme);
        Assert.True(ClockPalettes.All.Count >= 4);
    }
}
