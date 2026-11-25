using Chisel.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// The System section of the General settings: what closing the window does and how Chisel starts.
public partial class SystemSettingsViewModel : ViewModelBase
{
    private readonly ISystemSettingsRepository _repository;
    private bool _loading;

    public SystemSettingsViewModel(ISystemSettingsRepository repository)
    {
        _repository = repository;
        Current = repository.Get();
        _loading = true;
        CloseToTray = Current.CloseToTray;
        StartMinimized = Current.StartMinimized;
        _loading = false;
    }

    // The settings as last saved.
    public SystemSettings Current { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuitOnClose))]
    public partial bool CloseToTray { get; set; }

    public bool QuitOnClose
    {
        get => !CloseToTray;
        set => CloseToTray = !value;
    }

    [ObservableProperty]
    public partial bool StartMinimized { get; set; }

    partial void OnCloseToTrayChanged(bool value) => Save(Current with { CloseToTray = value });

    partial void OnStartMinimizedChanged(bool value) => Save(Current with { StartMinimized = value });

    private void Save(SystemSettings settings)
    {
        if (_loading || settings == Current)
        {
            return;
        }
        Current = settings;
        _repository.Save(settings);
    }
}
