using Chisel.App.Startup;
using Chisel.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// The System section of the General settings: what closing the window does and how Chisel starts.
public partial class SystemSettingsViewModel : ViewModelBase
{
    private readonly ISystemSettingsRepository _repository;
    private readonly StartWithWindows? _startup;
    private bool _loading;

    // startup: the Run registry entry; null hides the Start with Windows option (captures and tests that don't need it).
    public SystemSettingsViewModel(ISystemSettingsRepository repository, StartWithWindows? startup = null)
    {
        _repository = repository;
        _startup = startup;
        Current = repository.Get();
        _loading = true;
        CloseToTray = Current.CloseToTray;
        StartMinimized = Current.StartMinimized;
        ShowStartupState();
        _loading = false;
    }

    public bool CanStartWithWindows => _startup?.IsAvailable == true;

    // Shows the registry as it really is, which may differ from what was asked (a policy, or a change made outside Chisel).
    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial string? StartupError { get; set; }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading || _startup is null)
        {
            return;
        }
        var error = _startup.SetEnabled(value);
        _loading = true;
        ShowStartupState();
        _loading = false;
        StartupError = error ?? StartupError;
    }

    private void ShowStartupState()
    {
        if (_startup is null)
        {
            return;
        }
        var state = _startup.Read();
        StartWithWindows = state.IsOn;
        StartupError = state.Error;
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
