using Avalonia.Media;
using Avalonia.Media.Immutable;
using Chisel.App.Models;
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

    // Unknown ids fall back to Default Minimal.
    public void Use(string? paletteId) => Palette = BlockPalettes.Get(paletteId);

    public BlockBrushes Brushes(string? colorKey)
    {
        var swatch = Palette.Get(colorKey);
        return new BlockBrushes(
            new ImmutableSolidColorBrush(swatch.Fill),
            new ImmutableSolidColorBrush(swatch.Stripe),
            new ImmutableSolidColorBrush(swatch.Text),
            new ImmutableSolidColorBrush(Palette.TimeText));
    }
}
