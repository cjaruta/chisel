using Chisel.Core.Media;
using Chisel.Core.Theme;

namespace Chisel.Core.Tests.Media;

public class AlbumPaletteTests
{
    private const int Size = 32;

    // Builds a 32x32 RGBA buffer; the function gives the color of each pixel (alpha 255 unless it returns null for transparent).
    private static byte[] Image(Func<int, int, Rgb?> pixel, int size = Size)
    {
        var data = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var i = (y * size + x) * 4;
                if (pixel(x, y) is { } c)
                {
                    data[i] = c.R;
                    data[i + 1] = c.G;
                    data[i + 2] = c.B;
                    data[i + 3] = 255;
                }
            }
        }
        return data;
    }

    private static IReadOnlyList<Rgb> Extract(byte[] data) => AlbumPalette.Extract(data, Size, Size);

    private static double HueDistance(Rgb a, Rgb b)
    {
        var d = Math.Abs(AlbumPalette.Hue(a) - AlbumPalette.Hue(b));
        return Math.Min(d, 360 - d);
    }

    [Fact]
    public void FlatColor_GivesThreeCopiesOfIt()
    {
        var red = new Rgb(200, 40, 40);
        var palette = Extract(Image((_, _) => red));
        Assert.Equal(3, palette.Count);
        Assert.All(palette, c => Assert.True(Rgb.Contrast(c, red) < 1.05));
    }

    [Fact]
    public void TwoColorSplit_KeepsBothColors()
    {
        var blue = new Rgb(30, 80, 220);
        var orange = new Rgb(240, 140, 30);
        var palette = Extract(Image((x, _) => x < Size / 2 ? blue : orange));
        Assert.Equal(3, palette.Count);
        Assert.Contains(palette, c => HueDistance(c, blue) < 15);
        Assert.Contains(palette, c => HueDistance(c, orange) < 15);
    }

    [Fact]
    public void ThreeVerticalBands_GiveThreeDistinctColors()
    {
        var red = new Rgb(220, 30, 40);
        var green = new Rgb(40, 190, 70);
        var blue = new Rgb(40, 70, 220);
        var palette = Extract(Image((x, _) => x < 11 ? red : x < 22 ? green : blue));
        foreach (var expected in new[] { red, green, blue })
        {
            Assert.Contains(palette, c => HueDistance(c, expected) < 15);
        }
    }

    [Fact]
    public void WhiteAndBlackBorders_AreIgnoredWhenThereIsColor()
    {
        var teal = new Rgb(20, 160, 170);
        var plum = new Rgb(150, 40, 150);
        var palette = Extract(Image((x, y) =>
            x < 3 || x >= Size - 3 ? new Rgb(255, 255, 255)
            : y < 3 || y >= Size - 3 ? new Rgb(0, 0, 0)
            : x < Size / 2 ? teal : plum));
        Assert.All(palette, c =>
        {
            Assert.True(AlbumPalette.Saturation(c) > 0.3, $"{c.ToHex()} is not colorful");
            Assert.True(AlbumPalette.Lightness(c) is > 0.12 and < 0.9, $"{c.ToHex()} is near black or white");
        });
    }

    [Fact]
    public void FullyTransparent_GivesNothing()
    {
        Assert.Empty(Extract(Image((_, _) => null)));
    }

    [Fact]
    public void TransparentPixels_AreIgnored()
    {
        var green = new Rgb(30, 180, 60);
        var palette = Extract(Image((x, _) => x < 20 ? null : green));
        Assert.All(palette, c => Assert.True(HueDistance(c, green) < 15));
    }

    [Fact]
    public void Monochrome_GivesGreysThatDifferInLightness()
    {
        var palette = Extract(Image((x, _) => x < 11 ? new Rgb(40, 40, 40) : x < 22 ? new Rgb(128, 128, 128) : new Rgb(215, 215, 215)));
        Assert.Equal(3, palette.Count);
        Assert.All(palette, c => Assert.True(AlbumPalette.Saturation(c) < 0.05));
        Assert.True(palette.Max(AlbumPalette.Lightness) - palette.Min(AlbumPalette.Lightness) > 0.3);
    }

    [Fact]
    public void AllBlackOrWhite_StillGivesColors()
    {
        var palette = Extract(Image((x, _) => x < 16 ? new Rgb(0, 0, 0) : new Rgb(255, 255, 255)));
        Assert.Equal(3, palette.Count);
    }

    [Fact]
    public void SameImage_GivesTheSameResultEveryTime()
    {
        var rng = new Random(3);
        var noise = Image((_, _) => new Rgb((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256)));
        Assert.Equal(Extract(noise), Extract(noise));
    }

    [Fact]
    public void PatchFallback_AveragesLeftMiddleAndRightCenterPatches()
    {
        // A flat-looking image whose three center patches differ slightly: the clusters collapse into one, so the patches decide.
        var left = new Rgb(100, 100, 100);
        var mid = new Rgb(104, 104, 104);
        var right = new Rgb(108, 108, 108);
        var palette = Extract(Image((x, y) =>
        {
            var inRow = y is >= 15 and <= 17;
            if (inRow && x is >= 1 and <= 3) return left;
            if (inRow && x is >= 15 and <= 17) return mid;
            if (inRow && x is >= 28 and <= 30) return right;
            return new Rgb(104, 104, 104);
        }));
        Assert.Equal([left, mid, right], palette);
    }

    [Fact]
    public void Ordering_PutsTheDominantColorFirstThenByHueDistance()
    {
        var red = new Rgb(220, 30, 40);       // dominant (most pixels)
        var orange = new Rgb(230, 130, 30);   // near red in hue
        var blue = new Rgb(40, 70, 220);      // far from red
        var palette = Extract(Image((x, _) => x < 16 ? red : x < 24 ? blue : orange));
        Assert.True(HueDistance(palette[0], red) < 15);
        Assert.True(HueDistance(palette[1], orange) < 15);
        Assert.True(HueDistance(palette[2], blue) < 15);
    }
}
