using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Chisel.App.Theming;
using Chisel.App.ViewModels;
using Chisel.Core.Settings;
using Chisel.Core.Theme;

namespace Chisel.App.Tests.ViewModels;

public class ThemeSettingsTests
{
    private sealed class MemoryRepository(UserSettings? settings) : IUserSettingsRepository
    {
        public UserSettings? Current { get; private set; } = settings;
        public UserSettings? Get() => Current;
        public void Save(UserSettings value) => Current = value;
    }

    private static UserSettings Stored() => new("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));

    private static (SettingsViewModel Vm, MemoryRepository Repo, ThemeService Theme) Create(UserSettings? stored = null)
    {
        var repo = new MemoryRepository(stored ?? Stored());
        var theme = new ThemeService();
        return (new SettingsViewModel(repo, new TimeDisplay(), theme: theme), repo, theme);
    }

    [AvaloniaFact]
    public void ExistingUsers_LoadAsDefaultModeWithTheOrangeAccent()
    {
        var (vm, _, theme) = Create();

        Assert.Equal("default", vm.SelectedThemeMode.Id);
        Assert.Equal("#F5A524", vm.AccentHex);
        Assert.Equal("#F5A524", theme.Accent.ToHex());
        Assert.Equal("default", theme.Mode.Id);
        Assert.Contains(vm.AccentSwatches, s => s.IsSelected && s.Key == "#F5A524");
    }

    [AvaloniaFact]
    public void SavedModeAndAccent_AreAppliedWhenTheSettingsLoad()
    {
        var (_, _, theme) = Create(Stored().WithThemeMode("plum").WithAccentColor("#4C9AF0"));

        Assert.Equal("plum", theme.Mode.Id);
        Assert.Equal("#4C9AF0", theme.Accent.ToHex());
    }

    [AvaloniaFact]
    public void ChoosingAMode_AppliesAndSavesIt()
    {
        var (vm, repo, theme) = Create();

        vm.SelectedThemeMode = vm.ThemeModeOptions.Single(o => o.Id == "ocean");

        Assert.Equal("ocean", theme.Mode.Id);
        Assert.Equal("ocean", repo.Current!.ThemeMode);
    }

    [AvaloniaFact]
    public void ChoosingAnAccent_FromASwatchTheHexBoxOrThePicker_AppliesAndSavesIt()
    {
        var (vm, repo, theme) = Create();

        vm.SelectAccentCommand.Execute("#4C9AF0");
        Assert.Equal("#4C9AF0", repo.Current!.AccentColor);

        vm.AccentHex = "#2BB5A6";
        Assert.Equal("#2BB5A6", theme.Accent.ToHex());
        Assert.Equal("#2BB5A6", repo.Current!.AccentColor);

        vm.AccentValue = Color.FromRgb(0xA6, 0x6F, 0xD9);
        Assert.Equal("#A66FD9", vm.AccentHex);
        Assert.Equal("#A66FD9", repo.Current!.AccentColor);
    }

    [AvaloniaFact]
    public void TypingAnInvalidHex_ChangesNothing()
    {
        var (vm, repo, theme) = Create();

        vm.AccentHex = "#12";

        Assert.Equal("#F5A524", theme.Accent.ToHex());
        Assert.Null(repo.Current!.AccentColor);
    }

    [AvaloniaFact]
    public void Reset_GoesBackToTheDefaultOrangeAndForgetsTheChoice()
    {
        var (vm, repo, theme) = Create(Stored().WithAccentColor("#4C9AF0"));

        vm.ResetAccentCommand.Execute(null);

        Assert.Equal("#F5A524", theme.Accent.ToHex());
        Assert.Equal("#F5A524", vm.AccentHex);
        Assert.Null(repo.Current!.AccentColor);
    }

    [AvaloniaFact]
    public void AnAccentThatCannotBeReadOnTheMode_IsAdjustedAndSaidSo()
    {
        var (vm, _, theme) = Create(Stored().WithAccentColor("#000000"));

        Assert.True(vm.AccentAdjusted);
        Assert.True(Rgb.Contrast(theme.Tokens.Fill, theme.Mode.Card) >= 3);
        vm.SelectAccentCommand.Execute("#F5A524");
        Assert.False(vm.AccentAdjusted);
    }

    private sealed class FakePlatformTheme(bool isLight) : IPlatformTheme
    {
        public bool IsLight { get; private set; } = isLight;
        public event Action? Changed;

        public void Set(bool light)
        {
            IsLight = light;
            Changed?.Invoke();
        }
    }

    private static (SettingsViewModel Vm, MemoryRepository Repo, ThemeService Theme, FakePlatformTheme Windows) CreateWithWindows(
        UserSettings stored, bool windowsIsLight)
    {
        var repo = new MemoryRepository(stored);
        var windows = new FakePlatformTheme(windowsIsLight);
        var theme = new ThemeService(windows);
        return (new SettingsViewModel(repo, new TimeDisplay(), theme: theme), repo, theme, windows);
    }

    [AvaloniaFact]
    public void SystemMode_UsesTheWindowsSettingAndFollowsItLive()
    {
        var (vm, _, theme, windows) = CreateWithWindows(Stored().WithThemeMode("system"), windowsIsLight: true);

        Assert.Equal("system", vm.SelectedThemeMode.Id);
        Assert.Equal("light", theme.Mode.Id);

        windows.Set(light: false);

        Assert.Equal("default", theme.Mode.Id);
        Avalonia.Application.Current!.Resources.TryGetResource("WindowBackgroundBrush", null, out var brush);
        Assert.Equal(theme.Mode.Window.ToColor(), ((SolidColorBrush)brush!).Color);
    }

    [AvaloniaFact]
    public void AnExistingChoice_IsKeptWhenWindowsChanges()
    {
        var (vm, repo, theme, windows) = CreateWithWindows(Stored().WithThemeMode("ocean"), windowsIsLight: false);

        windows.Set(light: true);

        Assert.Equal("ocean", vm.SelectedThemeMode.Id);
        Assert.Equal("ocean", theme.Mode.Id);
        Assert.Equal("ocean", repo.Current!.ThemeMode);
    }

    [AvaloniaFact]
    public void ChoosingSystem_SavesItAndAppliesTheWindowsLook()
    {
        var (vm, repo, theme, _) = CreateWithWindows(Stored().WithThemeMode("plum"), windowsIsLight: true);

        vm.SelectedThemeMode = vm.ThemeModeOptions.Single(o => o.Id == "system");

        Assert.Equal("system", repo.Current!.ThemeMode);
        Assert.Equal("light", theme.Mode.Id);
    }
}
