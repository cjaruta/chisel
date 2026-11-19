namespace Chisel.Core.Theme;

// What the UI uses for one accent on one pair of surfaces.
// Fill: buttons, bars, markers (at least 3:1 against both surfaces). Text: accent-colored text (at least 4.5:1).
// OnAccent: text on a Fill, black or white. Hover and Pressed: Fill variants. Subtle: a faint tint of the card.
public sealed record AccentTokens(Rgb Chosen, Rgb Fill, Rgb Text, Rgb OnAccent, Rgb Hover, Rgb Pressed, Rgb Subtle);

public static class AccentDerivation
{
    public static readonly Rgb Black = new(0, 0, 0);
    public static readonly Rgb White = new(255, 255, 255);

    public static AccentTokens Derive(Rgb chosen, Rgb window, Rgb card)
    {
        var lightSurface = window.Luminance > 0.4;
        var fill = Adjust(chosen, 3, lightSurface, window, card);
        var text = Adjust(chosen, 4.5, lightSurface, window, card);
        var hover = Variant(fill, lightSurface ? -0.12 : 0.14, window, card);
        var pressed = Variant(fill, lightSurface ? -0.22 : -0.14, window, card);
        return new AccentTokens(chosen, fill, text, OnAccentFor(fill, hover, pressed), hover, pressed, card.Mix(chosen, 0.16));
    }

    // Black or white, whichever reads better on the worst of the given fills.
    private static Rgb OnAccentFor(params Rgb[] fills)
    {
        var black = fills.Min(f => Rgb.Contrast(Black, f));
        var white = fills.Min(f => Rgb.Contrast(White, f));
        return black >= white ? Black : White;
    }

    // Keeps the color when it already passes; otherwise steps its lightness away from the surfaces until it does.
    // Gray (no hue to keep) mixes toward the far end instead, so pure white on a light surface or black on a dark one still works.
    private static Rgb Adjust(Rgb color, double minimum, bool lightSurface, Rgb window, Rgb card)
    {
        if (Passes(color, minimum, window, card))
        {
            return color;
        }
        var direction = lightSurface ? -1.0 : 1.0;
        var far = lightSurface ? Black : White;
        var gray = color.R == color.G && color.G == color.B;
        for (var step = 1; step <= 100; step++)
        {
            var candidate = gray ? color.Mix(far, step / 100.0) : color.Shift(direction * step / 100.0);
            if (Passes(candidate, minimum, window, card))
            {
                return candidate;
            }
        }
        return far;
    }

    private static bool Passes(Rgb c, double minimum, Rgb window, Rgb card) =>
        Rgb.Contrast(c, window) >= minimum && Rgb.Contrast(c, card) >= minimum;

    // A hover or pressed fill. If the shift would break the 3:1 rule or the on-accent text, it shifts the other way,
    // and if that fails too it keeps the fill (the Hover/Pressed difference then comes from the fill moving the other way).
    private static Rgb Variant(Rgb fill, double amount, Rgb window, Rgb card)
    {
        foreach (var a in new[] { amount, -amount, amount / 2, -amount / 2 })
        {
            var candidate = fill.Shift(a);
            var onAccent = OnAccentFor(fill, candidate);
            if (Passes(candidate, 3, window, card) && Rgb.Contrast(onAccent, candidate) >= 4.5 && Rgb.Contrast(onAccent, fill) >= 4.5)
            {
                return candidate;
            }
        }
        return fill;
    }
}
