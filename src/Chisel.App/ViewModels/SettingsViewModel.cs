using Chisel.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IUserSettingsRepository _repository;
    private readonly TimeDisplay _display;

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

    public SettingsViewModel(IUserSettingsRepository repository, TimeDisplay display)
    {
        _repository = repository;
        _display = display;

        var saved = _repository.Get()?.ClockFormat ?? ClockFormat.TwentyFourHour;
        _display.Format = saved;
        UseTwelveHour = saved == ClockFormat.TwelveHour;
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
