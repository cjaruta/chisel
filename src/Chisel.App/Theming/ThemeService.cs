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

    // The saved choice: a mode id, or System (Mode is then Light or Default, following Windows).
    public string Choice { get; private set; } = ThemeModes.DefaultId;

    public event Action? Changed;

    private readonly IPlatformTheme? _platform;

    // platform: the Windows light/dark setting that System follows; without it System is dark.
    public ThemeService(IPlatformTheme? platform = null)
    {
        _platform = platform;
        if (platform is not null)
        {
            platform.Changed += () =>
            {
                if (Choice == ThemeModes.SystemId)
                {
                    Apply(Choice, Accent);
                }
            };
        }
    }

    public void Apply(string? modeId, string? accentHex) => Apply(modeId, ThemeModes.ResolveAccent(accentHex));

    public void Apply(string? choice, Rgb accent)
    {
        var mode = ThemeModes.Resolve(choice, _platform?.IsLight ?? false);
        Set(mode, accent);
        Choice = choice == ThemeModes.SystemId ? choice : mode.Id;
    }

    public void Apply(ThemeDefinition mode, Rgb accent)
    {
        Choice = mode.Id;
        Set(mode, accent);
    }

    private void Set(ThemeDefinition mode, Rgb accent)
    {
        Mode = mode;
        Accent = accent;
        Tokens = ThemeModes.Accent(mode, accent);

        if (Application.Current is { } app)
        {
            // Fluent first, and its palette before its variant: the variant decides how plain controls draw, and they read the
            // palette as the variant takes effect, so the new colors have to be in place before it changes.
            ApplyFluent(app, mode, Tokens);
            app.RequestedThemeVariant = mode.IsLight ? ThemeVariant.Light : ThemeVariant.Dark;
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

    private void ApplyFluent(Application app, ThemeDefinition m, AccentTokens a)
    {
        var fluent = app.Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluent is null)
        {
            return;
        }

        // Fills a palette with the mode's colors.
        void Fill(ColorPaletteResources p)
        {
            p.Accent = a.Fill.ToColor();
            p.RegionColor = m.Window.ToColor();
            p.AltHigh = m.Window.ToColor();
            p.AltMediumHigh = m.Window.ToColor(0xCC);
            p.AltMedium = m.Window.ToColor(0xCC);
            p.AltMediumLow = m.Window.ToColor(0x99);
            p.AltLow = m.Window.ToColor();
            p.BaseHigh = m.Text.ToColor();
            p.BaseMediumHigh = m.Text.Mix(m.TextSecondary, 0.4).ToColor();
            p.BaseMedium = m.TextSecondary.ToColor();
            p.BaseMediumLow = m.ControlBorder.ToColor();
            p.BaseLow = m.Raised.ToColor();
            p.ChromeAltLow = m.TextSecondary.ToColor();
            p.ChromeBlackHigh = m.Window.ToColor();
            p.ChromeBlackMedium = m.Window.ToColor(0xCC);
            p.ChromeBlackMediumLow = m.Window.ToColor(0x66);
            p.ChromeBlackLow = m.Window.ToColor(0x33);
            p.ChromeDisabledHigh = m.Border.ToColor();
            p.ChromeDisabledLow = m.TextDisabled.ToColor();
            p.ChromeHigh = m.ControlBorder.ToColor();
            p.ChromeLow = m.Card.ToColor();
            p.ChromeMedium = m.Raised.ToColor();
            p.ChromeMediumLow = m.Raised.ToColor();
            p.ChromeGray = m.TextSecondary.ToColor();
            p.ChromeWhite = m.Text.ToColor();
            p.ListLow = m.Raised.ToColor();
            p.ListMedium = m.Divider.ToColor();
            p.ErrorText = AccentDerivation.EnsureContrast(Rgb.Parse("#E06C75"), 4.5, m.IsLight, [m.Window, m.Card, m.Raised]).ToColor();
        }
        var next = new ColorPaletteResources();
        Fill(next);
        var slots = SlotsOf(next);
        if (_slots is { } previous)
        {
            Recolor(fluent, previous, slots);
        }
        _slots = slots;
        foreach (var variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            var palette = new ColorPaletteResources();
            Fill(palette);
            fluent.Palettes[variant] = palette;
        }
    }

    // The palette colors last given to Fluent, in a fixed order.
    private Color[]? _slots;

    private static Color[] SlotsOf(ColorPaletteResources p) =>
    [
        p.Accent, p.RegionColor, p.AltHigh, p.AltMediumHigh, p.AltMedium, p.AltMediumLow, p.AltLow, p.BaseHigh, p.BaseMediumHigh,
        p.BaseMedium, p.BaseMediumLow, p.BaseLow, p.ChromeAltLow, p.ChromeBlackHigh, p.ChromeBlackMedium, p.ChromeBlackMediumLow,
        p.ChromeBlackLow, p.ChromeDisabledHigh, p.ChromeDisabledLow, p.ChromeHigh, p.ChromeLow, p.ChromeMedium, p.ChromeMediumLow,
        p.ChromeGray, p.ChromeWhite, p.ListLow, p.ListMedium, p.ErrorText,
    ];

    // Fluent turns a palette into brushes the first time a variant is shown and keeps them, so a palette set afterwards does not
    // reach controls that are showing: the default text kept an earlier mode's color after a switch. The brushes are changed
    // where they are instead: each one holding a color of the old palette takes the matching color of the new one (keeping its
    // own transparency), and every control using it follows.
    private static void Recolor(FluentTheme fluent, Color[] old, Color[] next)
    {
        var map = new Dictionary<uint, Color?>();
        for (var i = 0; i < old.Length; i++)
        {
            var key = Rgb24(old[i]);
            var to = next[i];
            // The same old color standing for two different new ones cannot be told apart: leave those brushes alone.
            map[key] = map.TryGetValue(key, out var seen) && (seen is null || Rgb24(seen.Value) != Rgb24(to)) ? null : to;
        }
        var seenBrushes = new HashSet<SolidColorBrush>();
        foreach (var variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            Walk((IResourceDictionary)fluent.Resources, variant, brush =>
            {
                if (seenBrushes.Add(brush) && map.TryGetValue(Rgb24(brush.Color), out var to) && to is { } color)
                {
                    brush.Color = Color.FromArgb(brush.Color.A, color.R, color.G, color.B);
                }
            });
        }
    }

    private static uint Rgb24(Color c) => (uint)(c.R << 16 | c.G << 8 | c.B);

    private static void Walk(IResourceDictionary dictionary, ThemeVariant variant, Action<SolidColorBrush> visit)
    {
        foreach (var entry in dictionary)
        {
            if (entry.Value is SolidColorBrush brush)
            {
                visit(brush);
            }
        }
        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (merged is IResourceDictionary inner)
            {
                Walk(inner, variant, visit);
            }
        }
        if (dictionary.ThemeDictionaries.TryGetValue(variant, out var themed) && themed is IResourceDictionary themedDictionary)
        {
            Walk(themedDictionary, variant, visit);
        }
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
