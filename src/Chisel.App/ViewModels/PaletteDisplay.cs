using Avalonia.Media;
using Avalonia.Media.Immutable;
using Chisel.App.Models;
using Chisel.App.Theming;
using Chisel.Core.Theme;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// The brushes one block is drawn with. Immutable, so they can be shared across UI threads.
public sealed record BlockBrushes(IBrush Fill, IBrush Stripe, IBrush Text, IBrush TimeText);

// Shared by every view model that shows block colors, like TimeDisplay is for times.
// Changing the palette makes them all re-resolve their brushes, so it applies instantly.
public partial class PaletteDisplay : ViewModelBase
{
    [ObservableProperty]
    public partial BlockPalette Palette { get; private set; } = BlockPalettes.Minimal;

    // The theme mode the blocks are drawn on: a light mode draws them as soft tints with dark text (see LightBlockLook).
    [ObservableProperty]
    public partial ThemeDefinition Mode { get; private set; } = ThemeModes.Default;

    public void UseMode(ThemeDefinition mode) => Mode = mode;

    // Unknown ids fall back to Default Minimal.
    public void Use(string? paletteId) => Palette = BlockPalettes.Get(paletteId);

    public BlockBrushes Brushes(string? colorKey)
    {
        var swatch = Palette.Get(colorKey);
        if (Mode.IsLight)
        {
            var look = LightBlockLook.For(new Rgb(swatch.Stripe.R, swatch.Stripe.G, swatch.Stripe.B), Mode);
            return new BlockBrushes(look.Fill.ToBrush(), look.Stripe.ToBrush(), look.Text.ToBrush(), look.TimeText.ToBrush());
        }
        return new BlockBrushes(
            new ImmutableSolidColorBrush(swatch.Fill),
            new ImmutableSolidColorBrush(swatch.Stripe),
            new ImmutableSolidColorBrush(swatch.Text),
            new ImmutableSolidColorBrush(Palette.TimeText));
    }
}
