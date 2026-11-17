using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Chisel.App.Models;
using Chisel.Core.Schedule;

namespace Chisel.App.Tests.Models;

// The block color palettes: data per palette (id, name, and fill/stripe/text for each color key),
// all readable on the dark theme, with Default Saturated clearly more vivid and distinguishable.
public class BlockPaletteTests
{
    // WCAG AA for normal-size text.
    private const double MinimumTextContrast = 4.5;

    // WCAG non-text contrast, for the current-block outline against the block it surrounds.
    private const double MinimumOutlineContrast = 3;

    // CIE76 color difference in Lab. About 2.3 is barely noticeable; around 10 is clearly different.
    // Saturated stripes must be far apart (Default Minimal's closest pair, slate/lavender, is only about 15),
    // and even its dark fills must stay apart (Default Minimal's closest fills are about 4).
    private const double MinimumSaturatedStripeDistance = 30;
    private const double MinimumSaturatedFillDistance = 12;

    public static TheoryData<string> PaletteIds => new(BlockPalettes.All.Select(p => p.Id));

    [Fact]
    public void Contrast_BlackOnWhite_IsTwentyOne()
    {
        Assert.Equal(21, ColorContrast.Ratio(Colors.Black, Colors.White), 2);
    }

    [Fact]
    public void Contrast_SameColor_IsOne()
    {
        Assert.Equal(1, ColorContrast.Ratio(Color.Parse("#7D93C4"), Color.Parse("#7D93C4")), 6);
    }

    [Fact]
    public void Distance_SameColorIsZeroAndBlackToWhiteIsOneHundred()
    {
        Assert.Equal(0, ColorDistance.DeltaE76(Color.Parse("#7D93C4"), Color.Parse("#7D93C4")), 6);
        Assert.Equal(100, ColorDistance.DeltaE76(Colors.Black, Colors.White), 0);
    }

    [Fact]
    public void Palettes_AreMinimalThenSaturatedWithUniqueIds()
    {
        Assert.Equal(new[] { "Default Minimal", "Default Saturated" }, BlockPalettes.All.Select(p => p.Name));
        Assert.Equal(BlockPalettes.All.Count, BlockPalettes.All.Select(p => p.Id).Distinct().Count());
        Assert.Same(BlockPalettes.Minimal, BlockPalettes.All[0]);
        Assert.Same(BlockPalettes.Saturated, BlockPalettes.All[1]);
    }

    [Theory]
    [MemberData(nameof(PaletteIds))]
    public void EveryPalette_DefinesAllEightKeysInOrder(string id)
    {
        Assert.Equal(BlockColor.Keys, BlockPalettes.Get(id).Swatches.Select(s => s.Key));
    }

    [Theory]
    [MemberData(nameof(PaletteIds))]
    public void EveryPalette_TitleAndTimeTextAreReadableOnEveryFill(string id)
    {
        var palette = BlockPalettes.Get(id);

        Assert.All(palette.Swatches, swatch =>
        {
            Assert.True(ColorContrast.Ratio(swatch.Text, swatch.Fill) >= MinimumTextContrast,
                $"{id} {swatch.Key} title {ColorContrast.Ratio(swatch.Text, swatch.Fill):0.00}:1");
            Assert.True(ColorContrast.Ratio(palette.TimeText, swatch.Fill) >= MinimumTextContrast,
                $"{id} {swatch.Key} time {ColorContrast.Ratio(palette.TimeText, swatch.Fill):0.00}:1");
        });
    }

    // Uses the outline color the app actually draws the current block with.
    [AvaloniaFact]
    public void EveryPalette_TheCurrentBlockOutlineStandsOutOnEveryFill()
    {
        Assert.True(Application.Current!.TryFindResource("CurrentBlockOutlineColor", out var found));
        var outline = (Color)found!;

        Assert.All(BlockPalettes.All.SelectMany(p => p.Swatches.Select(s => (p.Id, s))), item =>
            Assert.True(ColorContrast.Ratio(outline, item.s.Fill) >= MinimumOutlineContrast, $"{item.Id} {item.s.Key}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("neon")]
    public void Get_UnknownPaletteId_FallsBackToDefaultMinimal(string? id)
    {
        Assert.Same(BlockPalettes.Minimal, BlockPalettes.Get(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("neon")]
    public void Swatch_UnknownKey_FallsBackToTheDefaultColor(string? key)
    {
        Assert.Equal(BlockColor.Default, BlockPalettes.Saturated.Get(key).Key);
    }

    // Default Minimal is exactly the palette from before palettes could be chosen.
    [Theory]
    [InlineData("slate", "#7D93C4", "#363D4F")]
    [InlineData("sage", "#8DB38A", "#394541")]
    [InlineData("teal", "#62AEB0", "#2F444A")]
    [InlineData("amber", "#D0A660", "#494237")]
    [InlineData("terracotta", "#CC7F66", "#493838")]
    [InlineData("rose", "#CC8BA0", "#493B46")]
    [InlineData("lavender", "#A693CF", "#3F3D52")]
    [InlineData("gray", "#9C978F", "#3D3E42")]
    public void Minimal_IsTheOriginalPalette(string key, string stripe, string fill)
    {
        var swatch = BlockPalettes.Minimal.Get(key);

        Assert.Equal((Color.Parse(stripe), Color.Parse(fill), Color.Parse("#ECEEF2")), (swatch.Stripe, swatch.Fill, swatch.Text));
        Assert.Equal(Color.Parse("#BCC1CA"), BlockPalettes.Minimal.TimeText);
    }

    [Fact]
    public void Saturated_EveryPairOfStripesIsClearlyDifferent()
    {
        AssertAllPairsApart(BlockPalettes.Saturated.Swatches.Select(s => (s.Key, s.Stripe)), MinimumSaturatedStripeDistance);
    }

    [Fact]
    public void Saturated_EveryPairOfFillsIsDifferent()
    {
        AssertAllPairsApart(BlockPalettes.Saturated.Swatches.Select(s => (s.Key, s.Fill)), MinimumSaturatedFillDistance);
    }

    [Fact]
    public void Saturated_IsMoreVividThanMinimalForEveryKey()
    {
        var card = Color.Parse("#1F222A");
        Assert.All(BlockColor.Keys, key =>
        {
            var minimal = BlockPalettes.Minimal.Get(key);
            var saturated = BlockPalettes.Saturated.Get(key);
            Assert.True(saturated.Stripe.ToHsl().S > minimal.Stripe.ToHsl().S, $"{key} stripe saturation");
            // A stronger tint: the fill sits further from the plain card background.
            Assert.True(ColorDistance.DeltaE76(saturated.Fill, card) > ColorDistance.DeltaE76(minimal.Fill, card), $"{key} fill tint");
        });
    }

    private static void AssertAllPairsApart(IEnumerable<(string Key, Color Color)> colors, double minimum)
    {
        var list = colors.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                var distance = ColorDistance.DeltaE76(list[i].Color, list[j].Color);
                Assert.True(distance >= minimum, $"{list[i].Key}/{list[j].Key} are only {distance:0.0} apart");
            }
        }
    }
}
