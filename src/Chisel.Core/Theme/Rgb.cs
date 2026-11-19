using System.Globalization;

namespace Chisel.Core.Theme;

// An opaque color with the few operations the theme needs: hex, WCAG contrast and lightness changes.
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static bool TryParse(string? hex, out Rgb color)
    {
        color = default;
        var s = hex?.Trim().TrimStart('#');
        if (s is null || (s.Length != 3 && s.Length != 6) || !s.All(Uri.IsHexDigit))
        {
            return false;
        }
        if (s.Length == 3)
        {
            s = string.Concat(s.Select(c => $"{c}{c}"));
        }
        color = new Rgb(
            byte.Parse(s[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(s[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(s[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        return true;
    }

    public static Rgb Parse(string hex) => TryParse(hex, out var c) ? c : throw new FormatException($"Not a color: {hex}");

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public double Luminance
    {
        get
        {
            static double Channel(byte v)
            {
                var c = v / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(R) + 0.7152 * Channel(G) + 0.0722 * Channel(B);
        }
    }

    public static double Contrast(Rgb a, Rgb b)
    {
        var (hi, lo) = a.Luminance >= b.Luminance ? (a.Luminance, b.Luminance) : (b.Luminance, a.Luminance);
        return (hi + 0.05) / (lo + 0.05);
    }

    // amount 0 gives this color, 1 gives other.
    public Rgb Mix(Rgb other, double amount) => new(
        (byte)Math.Round(R + (other.R - R) * amount),
        (byte)Math.Round(G + (other.G - G) * amount),
        (byte)Math.Round(B + (other.B - B) * amount));

    // Moves toward white (positive) or black (negative) by a fraction; keeps the hue.
    public Rgb Shift(double amount) => amount >= 0 ? Mix(new Rgb(255, 255, 255), amount) : Mix(new Rgb(0, 0, 0), -amount);
}
