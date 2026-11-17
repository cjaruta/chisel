using System.Collections.Generic;
using System.Linq;
using Chisel.App.Models;
using Chisel.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IUserSettingsRepository _repository;
    private readonly TimeDisplay _display;
    private readonly PaletteDisplay _palette;

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

    public SettingsViewModel(IUserSettingsRepository repository, TimeDisplay display, PaletteDisplay? palette = null)
    {
        _repository = repository;
        _display = display;
        _palette = palette ?? new PaletteDisplay();

        var stored = _repository.Get();
        var saved = stored?.ClockFormat ?? ClockFormat.TwentyFourHour;
        _display.Format = saved;
        UseTwelveHour = saved == ClockFormat.TwelveHour;

        // Unknown ids resolve to Default Minimal.
        _palette.Use(stored?.SchedulePalette);
        SelectedPalette = PaletteOptions.Single(o => o.Id == _palette.Palette.Id);
    }

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
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private void Close() => IsOpen = false;
}
