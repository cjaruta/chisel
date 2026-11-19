using Chisel.Core.Media;
using Chisel.Core.Theme;

namespace Chisel.Core.Tests.Media;

public class AlbumGradientTests
{
    public static IEnumerable<object[]> Modes => ThemeModes.All.Select(m => new object[] { m.Id });

    private static readonly Rgb[][] Palettes =
    [
        [new(255, 255, 255), new(255, 255, 255), new(255, 255, 255)],
        [new(0, 0, 0), new(0, 0, 0), new(0, 0, 0)],
        [new(255, 230, 0), new(255, 200, 0), new(250, 255, 60)],
        [new(255, 0, 255), new(0, 255, 255), new(57, 255, 20)],
        [new(255, 0, 0), new(0, 0, 255), new(0, 255, 0)],
        [new(128, 128, 128), new(30, 30, 30), new(220, 220, 220)],
        [new(20, 60, 160), new(90, 30, 120), new(10, 120, 110)],
    ];

    private static readonly string[] Accents = [ThemeModes.DefaultAccent, "#FFFFFF", "#000000", "#0A1A5C", "#FFE600"];

    [Theory]
    [MemberData(nameof(Modes))]
    public void Adjust_KeepsEveryStopInTheModesLightnessRange(string id)
    {
        var mode = ThemeModes.ById(id);
        var accent = ThemeModes.Accent(mode, Rgb.Parse(ThemeModes.DefaultAccent)).Fill;
        foreach (var palette in Palettes)
        {
            var adjusted = AlbumGradient.Adjust(palette, mode, accent);
            Assert.Equal(3, adjusted.Count);
            foreach (var color in adjusted)
            {
                var lightness = AlbumPalette.Lightness(color);
                if (mode.IsLight)
                {
                    Assert.True(lightness >= 0.7, $"{id}: {color.ToHex()} is too dark for a light mode");
                }
                else
                {
                    Assert.True(lightness <= 0.4, $"{id}: {color.ToHex()} is too light for a dark mode");
                }
            }
        }
    }

    [Fact]
    public void Adjust_LeavesAColorAlreadyInRangeNearlyAlone()
    {
        var mode = ThemeModes.Default;
        var accent = ThemeModes.Accent(mode, Rgb.Parse(ThemeModes.DefaultAccent)).Fill;
        var color = new Rgb(40, 60, 110);
        var adjusted = AlbumGradient.Adjust([color, color, color], mode, accent)[0];
        Assert.True(HueClose(color, adjusted));
    }

    private static bool HueClose(Rgb a, Rgb b) => Math.Abs(AlbumPalette.Hue(a) - AlbumPalette.Hue(b)) < 8;

    // Text (primary and secondary) needs 4.5:1 and controls (icons, accent fill used by the progress bar and bars) 3:1
    // against every stop, for every mode, accent and cover.
    [Theory]
    [MemberData(nameof(Modes))]
    public void Adjust_KeepsTextAndControlsReadableOnEveryStop(string id)
    {
        var mode = ThemeModes.ById(id);
        foreach (var accentHex in Accents)
        {
            var tokens = ThemeModes.Accent(mode, Rgb.Parse(accentHex));
            foreach (var palette in Palettes)
            {
                foreach (var stop in AlbumGradient.Adjust(palette, mode, tokens.Fill))
                {
                    Assert.True(Rgb.Contrast(mode.Text, stop) >= 4.5, $"{id}/{accentHex}: primary text on {stop.ToHex()}");
                    Assert.True(Rgb.Contrast(AlbumGradient.SecondaryText(mode), stop) >= 4.5, $"{id}/{accentHex}: secondary text on {stop.ToHex()}");
                    Assert.True(Rgb.Contrast(mode.Text, stop) >= 3, $"{id}: icons on {stop.ToHex()}");
                    Assert.True(Rgb.Contrast(tokens.Fill, stop) >= 3, $"{id}/{accentHex}: accent fill on {stop.ToHex()}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Fallback_IsReadableAndBuiltFromTheSurfaceAndAccent(string id)
    {
        var mode = ThemeModes.ById(id);
        var tokens = ThemeModes.Accent(mode, Rgb.Parse(ThemeModes.DefaultAccent));
        var fallback = AlbumGradient.Fallback(mode, tokens.Fill);
        Assert.Equal(3, fallback.Count);
        Assert.NotEqual(fallback[0], fallback[2]);
        foreach (var stop in fallback)
        {
            Assert.True(Rgb.Contrast(mode.Text, stop) >= 4.5);
            Assert.True(Rgb.Contrast(AlbumGradient.SecondaryText(mode), stop) >= 4.5);
            Assert.True(Rgb.Contrast(tokens.Fill, stop) >= 3);
        }
    }

    [Fact]
    public void PaletteCache_KeepsTheMostRecentlyUsedEntries()
    {
        var cache = new PaletteCache(2);
        Rgb[] a = [new(1, 1, 1)], b = [new(2, 2, 2)], c = [new(3, 3, 3)];
        cache.Add("a", a);
        cache.Add("b", b);
        Assert.Same(a, cache.Get("a")); // a is now the freshest
        cache.Add("c", c);              // evicts b
        Assert.Null(cache.Get("b"));
        Assert.Same(a, cache.Get("a"));
        Assert.Same(c, cache.Get("c"));
    }
}
