using System.Collections.Generic;
using Avalonia.Media;
using Chisel.App.Theming;
using Chisel.Core.Theme;

namespace Chisel.App.ViewModels;

// A theme mode in the settings panel: its name and a strip of its window, card, raised and text colors to compare before choosing.
public sealed class ThemeModeOption
{
    public ThemeModeOption(ThemeDefinition mode)
    {
        Id = mode.Id;
        Name = mode.Name;
        Preview = new IBrush[] { mode.Window.ToBrush(), mode.Card.ToBrush(), mode.Raised.ToBrush(), mode.TextSecondary.ToBrush(), mode.Text.ToBrush() };
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<IBrush> Preview { get; }
}
