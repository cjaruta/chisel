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
}
