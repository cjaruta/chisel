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

    [ObservableProperty]
    public partial bool AlbumGradient { get; set; } = true;

    [ObservableProperty]
    public partial bool AnimateGradient { get; set; } = true;

    public MediaSettingsViewModel(IUserSettingsRepository? repository)
    {
        _repository = repository;
        ScrollVolume = repository?.Get()?.MediaScrollVolume ?? false;
        ShowVisualizer = repository?.Get()?.MediaVisualizer ?? true;
        ShowVolume = repository?.Get()?.MediaVolume ?? true;
        AlbumGradient = repository?.Get()?.MediaAlbumGradient ?? true;
        AnimateGradient = repository?.Get()?.MediaAnimateGradient ?? true;
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

    partial void OnAlbumGradientChanged(bool value)
    {
        var settings = _repository?.Get();
        if (settings is not null && settings.MediaAlbumGradient != value)
        {
            _repository!.Save(settings.WithMediaAlbumGradient(value));
        }
    }

    partial void OnAnimateGradientChanged(bool value)
    {
        var settings = _repository?.Get();
        if (settings is not null && settings.MediaAnimateGradient != value)
        {
            _repository!.Save(settings.WithMediaAnimateGradient(value));
        }
    }
}
