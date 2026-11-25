using System;
using Chisel.App.Services;
using Chisel.App.Startup;
using Chisel.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// The System section of the General settings: what closing the window does and how Chisel starts.
public partial class SystemSettingsViewModel : ViewModelBase
{
    private readonly ISystemSettingsRepository _repository;
    private readonly StartWithWindows? _startup;
    private bool _loading;
    private IHotkeyService? _hotkeys;
    private Action<string>? _hotkeyFailed;
    private Hotkey? _registered;

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

    // The show/hide shortcut as shown in its box: "Ctrl+Alt+C", or "Off".
    public string HotkeyText => IsCapturingHotkey ? "Press the new shortcut…" : HotkeyOn ? Current.Hotkey : "Off";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyText))]
    public partial bool IsCapturingHotkey { get; set; }

    public bool HotkeyOn => Current.Hotkey.Length > 0;

    // Why the last shortcut couldn't be used; never left silent.
    [ObservableProperty]
    public partial string? HotkeyError { get; set; }

    // Connects the shortcut to Windows and registers the saved one. failed: told when the saved shortcut can't be registered at launch.
    public void AttachHotkeys(IHotkeyService hotkeys, Action<string>? failed = null)
    {
        _hotkeys = hotkeys;
        _hotkeyFailed = failed;
        if (!HotkeyOn)
        {
            return;
        }
        if (Hotkey.Parse(Current.Hotkey) is not { } saved)
        {
            ReportHotkeyFailure($"The saved shortcut \"{Current.Hotkey}\" couldn't be read. Set a new one in Settings.");
            return;
        }
        if (Register(saved) is { } error)
        {
            ReportHotkeyFailure(error);
        }
    }

    // While the capture box has focus the current shortcut is let go, so pressing it is captured instead of hiding the window.
    public void BeginHotkeyCapture()
    {
        IsCapturingHotkey = true;
        _hotkeys?.Unregister();
        _registered = null;
    }

    // A shortcut pressed in the capture box. True when it is now registered and saved; otherwise HotkeyError says why.
    public bool CaptureHotkey(HotkeyModifiers modifiers, string? key)
    {
        var (hotkey, invalid) = Hotkey.Validate(modifiers, key);
        if (hotkey is null)
        {
            HotkeyError = invalid;
            return false;
        }
        if (Register(hotkey) is { } error)
        {
            HotkeyError = error;
            return false;
        }
        HotkeyError = null;
        SaveHotkey(hotkey.ToString());
        return true;
    }

    // The capture box lost focus: the saved shortcut is registered again if nothing new took its place.
    public void EndHotkeyCapture()
    {
        IsCapturingHotkey = false;
        if (_registered is null && HotkeyOn && Hotkey.Parse(Current.Hotkey) is { } saved && Register(saved) is { } error)
        {
            HotkeyError = error;
        }
    }

    [RelayCommand]
    private void TurnOffHotkey()
    {
        _hotkeys?.Unregister();
        _registered = null;
        HotkeyError = null;
        SaveHotkey("");
    }

    // On exit.
    public void ReleaseHotkey()
    {
        _hotkeys?.Unregister();
        _registered = null;
    }

    private string? Register(Hotkey hotkey)
    {
        if (_hotkeys is null)
        {
            return null;
        }
        var result = _hotkeys.Register(hotkey);
        _registered = result == HotkeyRegistration.Registered ? hotkey : null;
        return result switch
        {
            HotkeyRegistration.Registered => null,
            HotkeyRegistration.InUse => $"{hotkey} is already used by another app or by Windows. Pick a different shortcut.",
            _ => $"Windows didn't accept {hotkey} as a shortcut. Pick a different one.",
        };
    }

    private void ReportHotkeyFailure(string message)
    {
        HotkeyError = message;
        _hotkeyFailed?.Invoke(message);
    }

    private void SaveHotkey(string hotkey)
    {
        Save(Current with { Hotkey = hotkey });
        OnPropertyChanged(nameof(HotkeyText));
        OnPropertyChanged(nameof(HotkeyOn));
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
