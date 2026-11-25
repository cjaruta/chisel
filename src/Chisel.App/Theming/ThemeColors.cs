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

    // A countdown event's color tag, a little lighter on dark themes. None gives the accent's place to the caller (transparent).
    public static Color CountdownTag(Chisel.Core.Countdown.CountdownColor tag, bool dark) => tag switch
    {
        Chisel.Core.Countdown.CountdownColor.Red => dark ? Color.FromRgb(0xF0, 0x7A, 0x76) : Color.FromRgb(0xC9, 0x3C, 0x37),
        Chisel.Core.Countdown.CountdownColor.Orange => dark ? Color.FromRgb(0xF2, 0xA6, 0x5A) : Color.FromRgb(0xC8, 0x6A, 0x14),
        Chisel.Core.Countdown.CountdownColor.Green => dark ? Color.FromRgb(0x6C, 0xC4, 0x8A) : Color.FromRgb(0x2E, 0x8A, 0x4B),
        Chisel.Core.Countdown.CountdownColor.Blue => dark ? Color.FromRgb(0x79, 0xA9, 0xF0) : Color.FromRgb(0x2F, 0x6B, 0xC4),
        Chisel.Core.Countdown.CountdownColor.Purple => dark ? Color.FromRgb(0xB4, 0x8C, 0xF0) : Color.FromRgb(0x7A, 0x48, 0xBE),
        _ => Color.FromArgb(0, 0, 0, 0),
    };

    public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
}
