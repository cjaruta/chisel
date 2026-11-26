using System.Collections.Generic;
using Avalonia.Media;
using Chisel.App.Theming;
using Chisel.Core.Theme;

namespace Chisel.App.ViewModels;

// A theme mode in the settings panel: its name and a strip of its window, card, raised and text colors to compare before choosing.
public sealed class ThemeModeOption
{
    public ThemeModeOption(ThemeDefinition mode)
        : this(mode.Id, mode.Name, new IBrush[] { mode.Window.ToBrush(), mode.Card.ToBrush(), mode.Raised.ToBrush(), mode.TextSecondary.ToBrush(), mode.Text.ToBrush() })
    {
    }

    private ThemeModeOption(string id, string name, IReadOnlyList<IBrush> preview)
    {
        Id = id;
        Name = name;
        Preview = preview;
    }

    // Follows Windows: its strip is half the light look and half the dark one.
    public static ThemeModeOption System()
    {
        var light = ThemeModes.Resolve(ThemeModes.SystemId, true);
        var dark = ThemeModes.Resolve(ThemeModes.SystemId, false);
        return new ThemeModeOption(ThemeModes.SystemId, "System",
            new IBrush[] { light.Window.ToBrush(), light.Card.ToBrush(), dark.Card.ToBrush(), dark.Window.ToBrush(), dark.Text.ToBrush() });
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<IBrush> Preview { get; }
}
