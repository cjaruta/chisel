using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// One round swatch in the block editor's color picker. Its brush follows the chosen palette.
public sealed partial class ColorSwatchOption : ObservableObject
{
    public ColorSwatchOption(string key, string name, IBrush brush)
    {
        Key = key;
        Name = name;
        Brush = brush;
    }

    public string Key { get; }
    public string Name { get; }

    [ObservableProperty]
    public partial IBrush Brush { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
