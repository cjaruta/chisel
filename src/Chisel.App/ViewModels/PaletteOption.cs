using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Chisel.App.Models;

namespace Chisel.App.ViewModels;

// A palette in the settings panel: its name and a strip of its eight colors to compare before choosing.
public sealed class PaletteOption
{
    public PaletteOption(BlockPalette palette)
    {
        Id = palette.Id;
        Name = palette.Name;
        Preview = palette.Swatches.Select(s => (IBrush)new ImmutableSolidColorBrush(s.Stripe)).ToList();
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<IBrush> Preview { get; }
}
