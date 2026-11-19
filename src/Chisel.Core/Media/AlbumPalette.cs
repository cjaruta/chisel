using Chisel.Core.Theme;

namespace Chisel.Core.Media;

// Picks three gradient colors from a small cover image (RGBA, 4 bytes per pixel, rows top to bottom, about 32x32).
//
// Method: k-means (k = 5, seeded so the same image always gives the same colors) on the opaque pixels. Clusters that are
// near black or near white are dropped unless nothing else is left. The rest are scored by population and saturation, and up
// to three that differ in hue or lightness are kept. They are ordered dominant first, then by growing hue distance from the
// dominant one, so neighbouring stops are close relatives.
// Fallback: when fewer than two distinct colors come out (flat, monochrome-ish or blank art), three 3x3 patches at the
// left-center, middle-center and right-center are averaged instead. A fully transparent image gives an empty list.
public static class AlbumPalette
{
    private const int K = 5;
    private const int Seed = 20240611;
    private const int Iterations = 12;

    public static double Hue(Rgb c) => ToHsl(c).H;

    public static double Saturation(Rgb c) => ToHsl(c).S;

    public static double Lightness(Rgb c) => ToHsl(c).L;

    public static IReadOnlyList<Rgb> Extract(ReadOnlySpan<byte> rgba, int width, int height)
    {
        var pixels = new List<(float R, float G, float B)>(width * height);
        for (var i = 0; i + 3 < rgba.Length && i / 4 < width * height; i += 4)
        {
            if (rgba[i + 3] >= 128)
            {
                pixels.Add((rgba[i], rgba[i + 1], rgba[i + 2]));
            }
        }
        if (pixels.Count == 0)
        {
            return [];
        }

        var picked = Pick(Cluster(pixels));
        if (picked.Count >= 2)
        {
            return Arrange(picked);
        }
        var overall = picked.Count == 1 ? picked[0] : Average(pixels);
        return Patches(rgba, width, height, overall);
    }

    private static List<(Rgb Color, int Count)> Cluster(List<(float R, float G, float B)> pixels)
    {
        var k = Math.Min(K, pixels.Count);
        var rng = new Random(Seed);
        var centers = new (float R, float G, float B)[k];
        centers[0] = pixels[rng.Next(pixels.Count)];
        var nearest = new float[pixels.Count];
        for (var c = 1; c < k; c++)
        {
            float total = 0;
            for (var i = 0; i < pixels.Count; i++)
            {
                nearest[i] = float.MaxValue;
                for (var j = 0; j < c; j++)
                {
                    nearest[i] = Math.Min(nearest[i], Distance(pixels[i], centers[j]));
                }
                total += nearest[i];
            }
            var target = (float)(rng.NextDouble() * total);
            var chosen = pixels.Count - 1;
            for (var i = 0; i < pixels.Count; i++)
            {
                target -= nearest[i];
                if (target <= 0)
                {
                    chosen = i;
                    break;
                }
            }
            centers[c] = pixels[total <= 0 ? 0 : chosen];
        }

        var assign = new int[pixels.Count];
        var sums = new (double R, double G, double B, int N)[k];
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            Array.Clear(sums);
            for (var i = 0; i < pixels.Count; i++)
            {
                var best = 0;
                var bestDistance = float.MaxValue;
                for (var j = 0; j < k; j++)
                {
                    var d = Distance(pixels[i], centers[j]);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = j;
                    }
                }
                assign[i] = best;
                sums[best] = (sums[best].R + pixels[i].R, sums[best].G + pixels[i].G, sums[best].B + pixels[i].B, sums[best].N + 1);
            }
            for (var j = 0; j < k; j++)
            {
                if (sums[j].N > 0)
                {
                    centers[j] = ((float)(sums[j].R / sums[j].N), (float)(sums[j].G / sums[j].N), (float)(sums[j].B / sums[j].N));
                }
            }
        }

        var result = new List<(Rgb, int)>();
        for (var j = 0; j < k; j++)
        {
            if (sums[j].N > 0)
            {
                result.Add((ToRgb(centers[j]), sums[j].N));
            }
        }
        return result;
    }

    private static float Distance((float R, float G, float B) a, (float R, float G, float B) b)
    {
        var dr = a.R - b.R;
        var dg = a.G - b.G;
        var db = a.B - b.B;
        return dr * dr + dg * dg + db * db;
    }

    private static Rgb ToRgb((float R, float G, float B) c) =>
        new((byte)Math.Round(c.R), (byte)Math.Round(c.G), (byte)Math.Round(c.B));

    private static Rgb Average(List<(float R, float G, float B)> pixels) =>
        ToRgb((pixels.Average(p => p.R), pixels.Average(p => p.G), pixels.Average(p => p.B)));

    // Drops near-black and near-white clusters (unless nothing else exists), then keeps up to three distinct ones, best score first.
    private static List<Rgb> Pick(List<(Rgb Color, int Count)> clusters)
    {
        var usable = clusters.Where(c => Lightness(c.Color) is >= 0.10 and <= 0.93).ToList();
        if (usable.Count == 0)
        {
            usable = clusters;
        }
        var picked = new List<Rgb>();
        foreach (var (color, _) in usable.OrderByDescending(c => c.Count * (0.25 + Saturation(c.Color))).ThenBy(c => c.Color.ToHex()))
        {
            if (picked.Count < 3 && picked.All(p => Distinct(p, color)))
            {
                picked.Add(color);
            }
        }
        return picked;
    }

    private static bool Distinct(Rgb a, Rgb b)
    {
        var (ha, sa, la) = ToHsl(a);
        var (hb, sb, lb) = ToHsl(b);
        return Math.Abs(la - lb) >= 0.15
            || Math.Abs(sa - sb) >= 0.3
            || (sa > 0.15 && sb > 0.15 && HueDistance(ha, hb) >= 30);
    }

    private static double HueDistance(double a, double b)
    {
        var d = Math.Abs(a - b);
        return Math.Min(d, 360 - d);
    }

    // Dominant first, then the rest by growing distance (hue for colors, lightness for greys). Two colors get a blend between them.
    private static Rgb[] Arrange(List<Rgb> picked)
    {
        var dominant = picked[0];
        var (hd, sd, ld) = ToHsl(dominant);
        double Far(Rgb c)
        {
            var (h, s, l) = ToHsl(c);
            return sd > 0.15 && s > 0.15 ? HueDistance(hd, h) : Math.Abs(ld - l) * 360;
        }
        var rest = picked.Skip(1).OrderBy(Far).ToList();
        if (rest.Count == 1)
        {
            return [dominant, Blend(dominant, rest[0]), rest[0]];
        }
        return [dominant, rest[0], rest[1]];
    }

    // Halfway between two colors, keeping the average saturation so it does not go grey.
    private static Rgb Blend(Rgb a, Rgb b)
    {
        var mixed = ToHsl(a.Mix(b, 0.5));
        var (_, sa, la) = ToHsl(a);
        var (_, sb, lb) = ToHsl(b);
        return FromHsl(mixed.H, (sa + sb) / 2, (la + lb) / 2);
    }

    // Three 3x3 patches at the left-center, middle-center and right-center, each averaged over its opaque pixels.
    private static IReadOnlyList<Rgb> Patches(ReadOnlySpan<byte> rgba, int width, int height, Rgb fallback)
    {
        var y = height / 2;
        var xs = new[] { width / 16, width / 2, width - 1 - width / 16 };
        var colors = new Rgb[3];
        for (var p = 0; p < 3; p++)
        {
            double r = 0, g = 0, b = 0;
            var n = 0;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var px = Math.Clamp(xs[p] + dx, 0, width - 1);
                    var py = Math.Clamp(y + dy, 0, height - 1);
                    var i = (py * width + px) * 4;
                    if (i + 3 < rgba.Length && rgba[i + 3] >= 128)
                    {
                        r += rgba[i];
                        g += rgba[i + 1];
                        b += rgba[i + 2];
                        n++;
                    }
                }
            }
            colors[p] = n == 0 ? fallback : new Rgb((byte)Math.Round(r / n), (byte)Math.Round(g / n), (byte)Math.Round(b / n));
        }
        return colors;
    }

    internal static (double H, double S, double L) ToHsl(Rgb c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        var d = max - min;
        if (d < 1e-9)
        {
            return (0, 0, l);
        }
        var s = d / (1 - Math.Abs(2 * l - 1));
        double h;
        if (max == r)
        {
            h = 60 * (((g - b) / d + 6) % 6);
        }
        else if (max == g)
        {
            h = 60 * ((b - r) / d + 2);
        }
        else
        {
            h = 60 * ((r - g) / d + 4);
        }
        return (h, Math.Clamp(s, 0, 1), l);
    }

    internal static Rgb FromHsl(double h, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l - c / 2;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        static byte B(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return new Rgb(B(r + m), B(g + m), B(b + m));
    }
}
