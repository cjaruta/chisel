using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Chisel.App.Models;
using Chisel.Core.Schedule;

namespace Chisel.App.Tests.Models;

// The block palette: one muted color per BlockColor key, with block text readable on every fill.
public class BlockPaletteTests
{
    // WCAG AA for normal-size text.
    private const double MinimumContrast = 4.5;

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
    public void Contrast_DoesNotDependOnOrder()
    {
        var a = Color.Parse("#ECEEF2");
        var b = Color.Parse("#363D4F");
        Assert.Equal(ColorContrast.Ratio(a, b), ColorContrast.Ratio(b, a), 6);
    }

    [Fact]
    public void Palette_HasEightSwatchesWithUniqueKeys()
    {
        Assert.Equal(8, BlockPalette.All.Count);
        Assert.Equal(BlockPalette.All.Count, BlockPalette.All.Select(s => s.Key).Distinct().Count());
    }

    [Fact]
    public void Palette_CoversExactlyTheStoredColorKeys()
    {
        Assert.Equal(BlockColor.Keys, BlockPalette.All.Select(s => s.Key));
    }

    [Fact]
    public void Palette_ColorsAreDistinct()
    {
        Assert.Equal(8, BlockPalette.All.Select(s => s.Stripe).Distinct().Count());
        Assert.Equal(8, BlockPalette.All.Select(s => s.Fill).Distinct().Count());
    }

    // Uses the text colors the app actually draws blocks with.
    [AvaloniaTheory]
    [InlineData("BlockTitleColor")]
    [InlineData("BlockTimeColor")]
    public void Palette_BlockTextIsReadableOnEveryFill(string textResource)
    {
        Assert.True(Application.Current!.TryFindResource(textResource, out var found), textResource);
        var text = (Color)found!;

        Assert.All(BlockPalette.All, swatch =>
            Assert.True(ColorContrast.Ratio(text, swatch.Fill) >= MinimumContrast,
                $"{textResource} on {swatch.Key} is {ColorContrast.Ratio(text, swatch.Fill):0.00}:1"));
    }

    [Fact]
    public void Palette_FillsStayDarkAndMuted()
    {
        // Fills are subtle tints: much darker than the stripe, never bright.
        Assert.All(BlockPalette.All, swatch =>
            Assert.True(ColorContrast.Ratio(swatch.Stripe, swatch.Fill) >= 2.5, swatch.Key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("neon")]
    public void Get_UnknownKey_FallsBackToTheDefault(string? key)
    {
        Assert.Equal(BlockColor.Default, BlockPalette.Get(key).Key);
    }

    [Fact]
    public void Get_KnownKey_ReturnsThatSwatch()
    {
        Assert.Equal("sage", BlockPalette.Get("sage").Key);
    }
}
