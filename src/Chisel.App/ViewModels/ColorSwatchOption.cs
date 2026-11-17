using Avalonia.Media;
using Avalonia.Media.Immutable;
using Chisel.App.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// One round swatch in the block editor's color picker.
public sealed partial class ColorSwatchOption : ObservableObject
{
    public ColorSwatchOption(BlockSwatch swatch)
    {
        Key = swatch.Key;
        Name = swatch.Name;
        Brush = new ImmutableSolidColorBrush(swatch.Stripe);
    }

    public string Key { get; }
    public string Name { get; }
    public IBrush Brush { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
