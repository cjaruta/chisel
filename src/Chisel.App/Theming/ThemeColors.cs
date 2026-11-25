using System;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Chisel.Core.Theme;

namespace Chisel.App.Theming;

// Conversions from the theme's colors to Avalonia's.
public static class ThemeColors
{
    public static Color ToColor(this Rgb c, byte alpha = 0xFF) => Color.FromArgb(alpha, c.R, c.G, c.B);

    public static IBrush ToBrush(this Rgb c) => new ImmutableSolidColorBrush(c.ToColor());

    public static Rgb ToRgb(this Color c) => new(c.R, c.G, c.B);

    public static Color WithAlpha(this Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);
    // The tone a system monitor gauge shifts to at high load: the accent moved part way toward a warm orange (lighter on dark
    // themes), so the change is noticeable without reading as an alarm.
    public static Color HighLoad(Color accent, bool dark) =>
        Mix(accent, dark ? Color.FromRgb(0xF2, 0xA6, 0x5A) : Color.FromRgb(0xD2, 0x69, 0x1E), 0.55);

    public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
}
