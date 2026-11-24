using System.Collections.Generic;
using System.Linq;
using Chisel.App.Models;
using Avalonia.Media;
using Chisel.App.Theming;
using Chisel.Core.Settings;
using Chisel.Core.Theme;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IUserSettingsRepository _repository;
    private readonly TimeDisplay _display;
    private readonly PaletteDisplay _palette;
    private bool _syncingAccent;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseTwentyFourHour))]
    public partial bool UseTwelveHour { get; set; }

    public bool UseTwentyFourHour
    {
        get => !UseTwelveHour;
        set => UseTwelveHour = !value;
    }

    // Schedule color palettes, each with a preview strip of its eight colors.
    public IReadOnlyList<PaletteOption> PaletteOptions { get; } = BlockPalettes.All.Select(p => new PaletteOption(p)).ToList();

    [ObservableProperty]
    public partial PaletteOption SelectedPalette { get; set; }

    // The theme service this panel drives; null in tests that only care about settings storage.
    public ThemeService? Theme { get; }

    // Theme modes, each with a strip previewing its surfaces and text.
    public IReadOnlyList<ThemeModeOption> ThemeModeOptions { get; } = ThemeModes.All.Select(m => new ThemeModeOption(m)).ToList();

    [ObservableProperty]
    public partial ThemeModeOption SelectedThemeMode { get; set; }

    // Accent presets as round swatches; the one matching the current accent has a ring.
    public IReadOnlyList<ColorSwatchOption> AccentSwatches { get; }

    // The accent as typed (#RRGGBB) and as a color for the picker. Changing either applies and saves at once.
    [ObservableProperty]
    public partial string AccentHex { get; set; } = ThemeModes.DefaultAccent;

    [ObservableProperty]
    public partial Color AccentValue { get; set; }

    // True when the chosen accent had to be lightened or darkened to stay readable in this mode.
    [ObservableProperty]
    public partial bool AccentAdjusted { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralTab), nameof(IsWidgetsTab), nameof(IsQuickLinksTab))]
    public partial SettingsTab SelectedTab { get; set; }

    public bool IsGeneralTab => SelectedTab == SettingsTab.General;
    public bool IsWidgetsTab => SelectedTab == SettingsTab.Widgets;
    public bool IsQuickLinksTab => SelectedTab == SettingsTab.QuickLinks;

    // Widgets that have options, and the one shown on the right of the Widgets tab.
    public IReadOnlyList<SettingsPage> WidgetPages { get; }

    [ObservableProperty]
    public partial SettingsPage? SelectedWidgetPage { get; set; }

    public SettingsViewModel(IUserSettingsRepository repository, TimeDisplay display, PaletteDisplay? palette = null,
        IReadOnlyList<SettingsPage>? pages = null, ThemeService? theme = null)
    {
        Theme = theme;
        _repository = repository;
        WidgetPages = pages ?? SettingsPages.Widgets;
        SelectedWidgetPage = WidgetPages.FirstOrDefault();
        _display = display;
        _palette = palette ?? new PaletteDisplay();

        var stored = _repository.Get();
        var saved = stored?.ClockFormat ?? ClockFormat.TwentyFourHour;
        _display.Format = saved;
        UseTwelveHour = saved == ClockFormat.TwelveHour;

        // Unknown ids resolve to Default Minimal.
        _palette.Use(stored?.SchedulePalette);
        SelectedPalette = PaletteOptions.Single(o => o.Id == _palette.Palette.Id);

        // Theme: applied here, before the first window exists, so nothing flashes in the wrong colors.
        AccentSwatches = ThemeModes.AccentPresets.Select(p => new ColorSwatchOption(p.Hex, p.Name, Rgb.Parse(p.Hex).ToBrush())).ToList();
        var mode = ThemeModes.ById(stored?.ThemeMode);
        var accent = ThemeModes.ResolveAccent(stored?.AccentColor);
        SelectedThemeMode = ThemeModeOptions.Single(o => o.Id == mode.Id);
        ShowAccent(accent);
        _palette.UseMode(mode);
        Theme?.Apply(mode, accent);
        UpdateAccentInfo();
    }

    // Shows an accent in the hex box, the picker and the swatch ring without applying it again.
    private void ShowAccent(Rgb accent)
    {
        _syncingAccent = true;
        AccentHex = accent.ToHex();
        AccentValue = accent.ToColor();
        foreach (var swatch in AccentSwatches)
        {
            swatch.IsSelected = swatch.Key == accent.ToHex();
        }
        _syncingAccent = false;
    }

    private void UpdateAccentInfo()
    {
        var mode = ThemeModes.ById(SelectedThemeMode?.Id);
        AccentAdjusted = Theme is not null && ThemeModes.Accent(mode, Theme.Accent).Fill != Theme.Accent;
    }

    // Applies and saves an accent chosen from any of the controls; null goes back to the default orange.
    private void ChooseAccent(Rgb accent, bool isDefault)
    {
        ShowAccent(accent);
        Theme?.Apply(Theme.Mode, accent);
        UpdateAccentInfo();
        var settings = _repository.Get();
        var saved = isDefault ? null : accent.ToHex();
        if (settings is not null && settings.AccentColor != saved)
        {
            _repository.Save(settings.WithAccentColor(saved));
        }
    }

    partial void OnSelectedThemeModeChanged(ThemeModeOption value)
    {
        if (value is null)
        {
            return;
        }
        _palette.UseMode(ThemeModes.ById(value.Id));
        if (Theme is null)
        {
            return;
        }
        Theme.Apply(ThemeModes.ById(value.Id), Theme.Accent);
        UpdateAccentInfo();
        var settings = _repository.Get();
        if (settings is not null && settings.ThemeMode != value.Id)
        {
            _repository.Save(settings.WithThemeMode(value.Id));
        }
    }

    partial void OnAccentHexChanged(string value)
    {
        if (!_syncingAccent && Rgb.TryParse(value, out var accent))
        {
            ChooseAccent(accent, false);
        }
    }

    partial void OnAccentValueChanged(Color value)
    {
        if (!_syncingAccent)
        {
            ChooseAccent(value.ToRgb(), false);
        }
    }

    [RelayCommand]
    private void SelectAccent(string hex)
    {
        if (Rgb.TryParse(hex, out var accent))
        {
            ChooseAccent(accent, hex == ThemeModes.DefaultAccent);
        }
    }

    [RelayCommand]
    private void ResetAccent() => ChooseAccent(ThemeModes.ResolveAccent(null), true);

    partial void OnSelectedPaletteChanged(PaletteOption value)
    {
        if (value is null)
        {
            return;
        }
        _palette.Use(value.Id);

        var settings = _repository.Get();
        if (settings is not null && settings.SchedulePalette != value.Id)
        {
            _repository.Save(settings.WithSchedulePalette(value.Id));
        }
    }

    partial void OnUseTwelveHourChanged(bool value)
    {
        var format = value ? ClockFormat.TwelveHour : ClockFormat.TwentyFourHour;
        _display.Format = format;

        var settings = _repository.Get();
        if (settings is not null && settings.ClockFormat != format)
        {
            _repository.Save(settings.WithClockFormat(format));
        }
    }

    [RelayCommand]
    private void SelectTab(SettingsTab tab) => SelectedTab = tab;

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private void Close() => IsOpen = false;
}
