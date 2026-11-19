using Chisel.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// The Media page in the Widgets settings: whether the widget shows its audio bars. Saved as soon as it changes.
public partial class MediaSettingsViewModel : ViewModelBase
{
    private readonly IUserSettingsRepository? _repository;

    [ObservableProperty]
    public partial bool ShowVisualizer { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowVolume { get; set; } = true;

    [ObservableProperty]
    public partial bool ScrollVolume { get; set; }

    public MediaSettingsViewModel(IUserSettingsRepository? repository)
    {
        _repository = repository;
        ScrollVolume = repository?.Get()?.MediaScrollVolume ?? false;
        ShowVisualizer = repository?.Get()?.MediaVisualizer ?? true;
        ShowVolume = repository?.Get()?.MediaVolume ?? true;
    }

    partial void OnShowVisualizerChanged(bool value)
    {
        var settings = _repository?.Get();
        if (settings is not null && settings.MediaVisualizer != value)
        {
            _repository!.Save(settings.WithMediaVisualizer(value));
        }
    }

    partial void OnScrollVolumeChanged(bool value)
    {
        var settings = _repository?.Get();
        if (settings is not null && settings.MediaScrollVolume != value)
        {
            _repository!.Save(settings.WithMediaScrollVolume(value));
        }
    }

    partial void OnShowVolumeChanged(bool value)
    {
        var settings = _repository?.Get();
        if (settings is not null && settings.MediaVolume != value)
        {
            _repository!.Save(settings.WithMediaVolume(value));
        }
    }
}
