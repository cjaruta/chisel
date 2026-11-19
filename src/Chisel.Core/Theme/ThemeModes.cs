namespace Chisel.Core.Theme;

// One complete palette. Surfaces go from the window at the back to raised controls at the front.
// Border is the quiet card outline; ControlBorder is the outline of inputs and check boxes (at least 3:1 against every surface).
// Overlay is the color of faint hover and edit-mode washes (white on dark modes, black on light).
public sealed record ThemeDefinition(
    string Id, string Name, bool IsLight,
    Rgb Window, Rgb Card, Rgb Raised, Rgb Border, Rgb ControlBorder, Rgb Divider,
    Rgb Text, Rgb TextSecondary, Rgb TextDisabled, Rgb Overlay);

public static class ThemeModes
{
    // The accent the app has always used for its edit-mode and now markers.
    public const string DefaultAccent = "#F5A524";
    public const string DefaultId = "default";

    private static ThemeDefinition Make(string id, string name, bool light, string window, string card, string raised, string border,
        string controlBorder, string divider, string text, string secondary, string disabled) =>
        new(id, name, light, Rgb.Parse(window), Rgb.Parse(card), Rgb.Parse(raised), Rgb.Parse(border), Rgb.Parse(controlBorder),
            Rgb.Parse(divider), Rgb.Parse(text), Rgb.Parse(secondary), Rgb.Parse(disabled), light ? AccentDerivation.Black : AccentDerivation.White);

    public static readonly ThemeDefinition Default = Make("default", "Default", false,
        "#14161A", "#1F222A", "#2B2E36", "#2A2E37", "#7A8190", "#3D4350", "#ECEEF2", "#9AA0AA", "#5A5F69");

    public static readonly IReadOnlyList<ThemeDefinition> All =
    [
        Default,
        Make("light", "Light", true,
            "#F3F4F6", "#FFFFFF", "#EBEDF1", "#E1E4E8", "#6F7785", "#D3D7DE", "#14161A", "#4B5260", "#9AA1AC"),
        Make("ultra-dark", "Ultra dark", false,
            "#000000", "#0B0B0C", "#161618", "#1E1E21", "#74747D", "#2A2A2E", "#F2F2F4", "#A0A0A8", "#5C5C63"),
        Make("ocean", "Ocean", false,
            "#0B1620", "#112433", "#19324A", "#1E3A52", "#7194AF", "#2A4A66", "#E6F1FA", "#9DB6CA", "#5A768C"),
        Make("forest", "Forest", false,
            "#0E1511", "#16231B", "#1F3326", "#26402F", "#729A83", "#34523F", "#E8F3EC", "#9DBAA8", "#5E7A68"),
        Make("plum", "Plum", false,
            "#17101C", "#231A2B", "#31243D", "#3A2B49", "#9C80B1", "#4A3860", "#F3EAF8", "#BBA3CA", "#7A6488"),
        Make("slate", "Slate", false,
            "#1B2028", "#252B35", "#313845", "#3A4250", "#8D99AC", "#4A5463", "#EEF1F5", "#A9B4C3", "#6C7787"),
    ];

    // Unknown or missing ids give Default.
    public static ThemeDefinition ById(string? id) => All.FirstOrDefault(m => m.Id == id) ?? Default;

    // A saved accent that is missing or not a color gives the default orange.
    public static Rgb ResolveAccent(string? hex) => Rgb.TryParse(hex, out var c) ? c : Rgb.Parse(DefaultAccent);

    public static AccentTokens Accent(ThemeDefinition mode, Rgb chosen) =>
        AccentDerivation.Derive(chosen, [mode.Window, mode.Card, mode.Raised], mode.IsLight);
}
