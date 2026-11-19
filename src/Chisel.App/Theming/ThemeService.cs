using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Chisel.Core.Theme;

namespace Chisel.App.Theming;

// Applies a theme mode and accent to the running app. It changes the Color of the brushes defined in Tokens.axaml in
// place (so every user of a brush follows, StaticResource or not), points Fluent's own palette at the same colors
// so standard controls match, and then asks every window to repaint so custom-drawn controls pick the change up.
public sealed class ThemeService
{
    public ThemeDefinition Mode { get; private set; } = ThemeModes.Default;
    public Rgb Accent { get; private set; } = ThemeModes.ResolveAccent(null);
    public AccentTokens Tokens { get; private set; } = ThemeModes.Accent(ThemeModes.Default, ThemeModes.ResolveAccent(null));

    public event Action? Changed;

    public void Apply(string? modeId, string? accentHex) => Apply(ThemeModes.ById(modeId), ThemeModes.ResolveAccent(accentHex));

    public void Apply(ThemeDefinition mode, Rgb accent)
    {
        Mode = mode;
        Accent = accent;
        Tokens = ThemeModes.Accent(mode, accent);

        if (Application.Current is { } app)
        {
            // Fluent first: its light or dark variant decides how plain controls draw before our brushes land.
            app.RequestedThemeVariant = mode.IsLight ? ThemeVariant.Light : ThemeVariant.Dark;
            ApplyFluent(app, mode, Tokens);
            ApplyBrushes(app, mode, Tokens);
            Repaint(app);
        }
        Changed?.Invoke();
    }

    private static void ApplyBrushes(Application app, ThemeDefinition m, AccentTokens a)
    {
        void Set(string key, Rgb color)
        {
            if (app.Resources.TryGetResource(key, null, out var found) && found is SolidColorBrush brush)
            {
                brush.Color = color.ToColor();
            }
            else
            {
                throw new InvalidOperationException($"Theme token {key} is missing from Tokens.axaml.");
            }
        }

        var surfaces = new[] { m.Window, m.Card, m.Raised };
        Set("WindowBackgroundBrush", m.Window);
        Set("CardBackgroundBrush", m.Card);
        Set("RaisedBrush", m.Raised);
        Set("EditBadgeBrush", m.Raised);
        Set("EditBarBrush", m.Card.Mix(m.Raised, 0.5));
        Set("EditPageBrush", m.Window.Mix(m.Card, 0.15));
        Set("GridLineBrush", m.Border);
        Set("HoverLineBrush", m.Divider);
        Set("ControlBorderBrush", m.ControlBorder);
        Set("TextBrush", m.Text);
        Set("DimTextBrush", m.TextSecondary);
        Set("DisabledTextBrush", m.TextDisabled);
        Set("AccentBrush", a.Fill);
        Set("AccentHoverBrush", a.Hover);
        Set("AccentPressedBrush", a.Pressed);
        Set("AccentTextBrush", a.Text);
        Set("OnAccentBrush", a.OnAccent);
        Set("AccentSubtleBrush", a.Fill);
        Set("AccentBorderBrush", a.Fill);
        Set("DragPreviewBrush", a.Fill);
        foreach (var wash in new[] { "HeaderBackgroundBrush", "HoverWashBrush", "EditHairlineBrush", "EditHairlineHoverBrush", "EditPillBrush" })
        {
            Set(wash, m.Overlay);
        }

        // Status marks keep their hue but are moved until they read on every surface of the mode.
        Set("CheckpointBrush", AccentDerivation.EnsureContrast(Rgb.Parse("#E06C75"), 3, m.IsLight, surfaces));
        Set("ReminderBrush", AccentDerivation.EnsureContrast(Rgb.Parse("#C9B77A"), 3, m.IsLight, surfaces));
    }

    private static void ApplyFluent(Application app, ThemeDefinition m, AccentTokens a)
    {
        var fluent = app.Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluent is null)
        {
            return;
        }

        ColorPaletteResources Palette() => new()
        {
            Accent = a.Fill.ToColor(),
            RegionColor = m.Window.ToColor(),
            AltHigh = m.Window.ToColor(),
            AltMediumHigh = m.Window.ToColor(0xCC),
            AltMedium = m.Window.ToColor(0xCC),
            AltMediumLow = m.Window.ToColor(0x99),
            AltLow = m.Window.ToColor(),
            BaseHigh = m.Text.ToColor(),
            BaseMediumHigh = m.Text.Mix(m.TextSecondary, 0.4).ToColor(),
            BaseMedium = m.TextSecondary.ToColor(),
            BaseMediumLow = m.ControlBorder.ToColor(),
            BaseLow = m.Raised.ToColor(),
            ChromeAltLow = m.TextSecondary.ToColor(),
            ChromeBlackHigh = m.Window.ToColor(),
            ChromeBlackMedium = m.Window.ToColor(0xCC),
            ChromeBlackMediumLow = m.Window.ToColor(0x66),
            ChromeBlackLow = m.Window.ToColor(0x33),
            ChromeDisabledHigh = m.Border.ToColor(),
            ChromeDisabledLow = m.TextDisabled.ToColor(),
            ChromeHigh = m.ControlBorder.ToColor(),
            ChromeLow = m.Card.ToColor(),
            ChromeMedium = m.Raised.ToColor(),
            ChromeMediumLow = m.Raised.ToColor(),
            ChromeGray = m.TextSecondary.ToColor(),
            ChromeWhite = m.Text.ToColor(),
            ListLow = m.Raised.ToColor(),
            ListMedium = m.Divider.ToColor(),
            ErrorText = AccentDerivation.EnsureContrast(Rgb.Parse("#E06C75"), 4.5, m.IsLight, [m.Window, m.Card, m.Raised]).ToColor(),
        };
        fluent.Palettes[ThemeVariant.Dark] = Palette();
        fluent.Palettes[ThemeVariant.Light] = Palette();
    }

    // Custom-drawn controls (the clock face, the drop border, the schedule grid) read brushes while painting, so they
    // need a nudge when only a brush color changed.
    private void Repaint(Application app)
    {
        _tracked.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var weak in _tracked.ToList())
        {
            if (weak.TryGetTarget(out var root))
            {
                Repaint(root);
            }
        }
    }

    private readonly List<WeakReference<Visual>> _tracked = [];

    // Windows to repaint when the theme changes.
    public void Track(Visual root)
    {
        if (!_tracked.Any(w => w.TryGetTarget(out var t) && ReferenceEquals(t, root)))
        {
            _tracked.Add(new WeakReference<Visual>(root));
        }
    }

    public static void Repaint(Visual root)
    {
        root.InvalidateVisual();
        foreach (var visual in root.GetVisualDescendants())
        {
            visual.InvalidateVisual();
        }
    }

    // Brush lookups for code that draws: the themed brush for a token key, or nothing visible if the key is unknown.
    public static IBrush Brush(Control control, string key)
    {
        if (control.TryFindResource(key, control.ActualThemeVariant, out var found) && found is IBrush brush)
        {
            return brush;
        }
        // A control that is not in a window yet cannot see the app's resources through its parents.
        return Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var app) == true && app is IBrush appBrush
            ? appBrush : Brushes.Transparent;
    }
}
