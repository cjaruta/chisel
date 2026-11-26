namespace Chisel.Core.Theme;

// How a schedule block is drawn in a light mode. The palettes' own fills are dark tints with light text, made for dark modes; on a
// light card they would be heavy dark slabs, and the dimmed past blocks would wash their text out. Here the fill is a soft tint of
// the block's stripe over the card, with dark text, and every part is moved until it reads on that fill.
public readonly record struct BlockLook(Rgb Fill, Rgb Stripe, Rgb Text, Rgb TimeText);

public static class LightBlockLook
{
    // How much of the stripe color tints the card to make the fill.
    public const double Tint = 0.24;

    public static BlockLook For(Rgb stripe, ThemeDefinition mode)
    {
        var surfaces = new[] { mode.Card };
        var fill = mode.Card.Mix(stripe, Tint);
        return new BlockLook(
            fill,
            AccentDerivation.EnsureContrast(stripe, 3, true, surfaces),
            AccentDerivation.EnsureContrast(mode.Text, 7, true, [fill]),
            AccentDerivation.EnsureContrast(mode.TextSecondary, 4.5, true, [fill]));
    }
}
