using Chisel.Core.Theme;

namespace Chisel.Core.Media;

// Turns the colors taken from a cover into a gradient that is safe behind the media widget in a given theme mode: lightness
// and saturation are clamped (darker range for dark modes, lighter for Light), then each stop is moved further toward black
// (or white in Light) until the widget's primary and secondary text meet 4.5:1 and the controls (icons, accent fill used by
// the progress bar and audio bars) meet 3:1 against it.
public static class AlbumGradient
{
    private const double DarkMinLightness = 0.10, DarkMaxLightness = 0.34, DarkMaxSaturation = 0.65;
    private const double LightMinLightness = 0.80, LightMaxLightness = 0.93, LightMaxSaturation = 0.70;
    private const double TextRatio = 4.5, ControlRatio = 3.0, Margin = 0.1;

    // Secondary text over the gradient: between the mode's text and its dim text, closer to the text, so the dim color
    // does not force the gradient to be almost black.
    public static Rgb SecondaryText(ThemeDefinition mode) => mode.Text.Mix(mode.TextSecondary, 0.25);

    public static IReadOnlyList<Rgb> Adjust(IReadOnlyList<Rgb> palette, ThemeDefinition mode, Rgb accentFill)
    {
        var result = new Rgb[palette.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = AdjustOne(palette[i], mode, accentFill);
        }
        return result;
    }

    private static Rgb AdjustOne(Rgb color, ThemeDefinition mode, Rgb accentFill)
    {
        var (h, s, l) = AlbumPalette.ToHsl(color);
        var adjusted = mode.IsLight
            ? AlbumPalette.FromHsl(h, Math.Min(s, LightMaxSaturation), Math.Clamp(l, LightMinLightness, LightMaxLightness))
            : AlbumPalette.FromHsl(h, Math.Min(s, DarkMaxSaturation), Math.Clamp(l, DarkMinLightness, DarkMaxLightness));
        var secondary = SecondaryText(mode);
        for (var step = 0; step < 40 && !Readable(adjusted, mode, secondary, accentFill); step++)
        {
            adjusted = adjusted.Shift(mode.IsLight ? 0.05 : -0.05);
        }
        return adjusted;
    }

    private static bool Readable(Rgb stop, ThemeDefinition mode, Rgb secondary, Rgb accentFill) =>
        Rgb.Contrast(mode.Text, stop) >= TextRatio + Margin
        && Rgb.Contrast(secondary, stop) >= TextRatio + Margin
        && Rgb.Contrast(accentFill, stop) >= ControlRatio + Margin;

    // No art, a failed decode or no session: the card surface warming toward the accent.
    public static IReadOnlyList<Rgb> Fallback(ThemeDefinition mode, Rgb accentFill) =>
        Adjust([mode.Card, mode.Card.Mix(accentFill, 0.14), mode.Raised.Mix(accentFill, 0.28)], mode, accentFill);
}
