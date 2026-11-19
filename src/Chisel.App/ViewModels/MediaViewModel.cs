using System;
using System.Threading.Tasks;
using Chisel.Core.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// What the media widget shows. It only listens to the service while the widget is on the dashboard (IsActive).
// The service raises Changed on the UI thread (the Windows one marshals it). Progress is the position the service
// last reported plus the time since, so it advances without asking the service again; Tick moves it along.
public sealed partial class MediaViewModel : ViewModelBase
{
    private readonly IMediaService _service;
    private readonly TimeProvider _time;
    private MediaSessionInfo? _info;
    private DateTimeOffset _readAt;
    private bool _active;

    public MediaViewModel(IMediaService service, TimeProvider time)
    {
        _service = service;
        _time = time;
    }

    // Starts and stops following the service; stopping drops everything held (including album art).
    public bool IsActive
    {
        get => _active;
        set
        {
            if (_active == value)
            {
                return;
            }
            _active = value;
            if (value)
            {
                _service.Changed += OnServiceChanged;
                Reload();
            }
            else
            {
                _service.Changed -= OnServiceChanged;
                _info = null;
                RaiseAll();
            }
        }
    }

    public bool HasSession => _info is not null;
    public string Title => _info is null ? "" : string.IsNullOrWhiteSpace(_info.Title) ? "Unknown title" : _info.Title;
    public string Artist => _info is null ? "" : !string.IsNullOrWhiteSpace(_info.Artist) ? _info.Artist : _info.AlbumArtist;
    public string Album => _info?.AlbumTitle ?? "";
    public string Source => MediaFormat.SourceName(_info?.SourceApp);
    public byte[]? ArtBytes => _info?.AlbumArt;
    public bool IsPlaying => _info?.State == MediaPlaybackState.Playing;

    public bool CanPrevious => Has(MediaControls.Previous);
    public bool CanNext => Has(MediaControls.Next);
    // The play/pause button does the opposite of the current state, so it needs that control.
    public bool CanPlayPause => Has(IsPlaying ? MediaControls.Pause : MediaControls.Play);

    public bool HasTimeline => _info is { Duration: { } d } && d > TimeSpan.Zero;
    public double ProgressFraction => HasTimeline ? MediaFormat.Progress(Position, _info!.Duration!.Value) : 0;
    public string ElapsedText => HasTimeline ? MediaFormat.FormatTime(Position) : "";
    public string DurationText => HasTimeline ? MediaFormat.FormatTime(_info!.Duration!.Value) : "";

    private TimeSpan Position
    {
        get
        {
            var position = _info?.Position ?? TimeSpan.Zero;
            if (IsPlaying)
            {
                position += _time.GetUtcNow() - _readAt;
            }
            var duration = _info?.Duration ?? TimeSpan.Zero;
            return position > duration ? duration : position < TimeSpan.Zero ? TimeSpan.Zero : position;
        }
    }

    private bool Has(MediaControls control) => _info is not null && _info.Controls.HasFlag(control);

    // Called often by the window's clock; only moves the progress while a track is playing.
    public void Tick()
    {
        if (_active && IsPlaying && HasTimeline)
        {
            OnPropertyChanged(nameof(ProgressFraction));
            OnPropertyChanged(nameof(ElapsedText));
        }
    }

    private void OnServiceChanged(object? sender, EventArgs e) => Reload();

    private void Reload()
    {
        _info = _service.Current;
        _readAt = _time.GetUtcNow();
        RaiseAll();
    }

    private byte[]? _shownArt;

    private void RaiseAll()
    {
        foreach (var name in new[]
        {
            nameof(HasSession), nameof(Title), nameof(Artist), nameof(Album), nameof(Source), nameof(IsPlaying),
            nameof(CanPrevious), nameof(CanNext), nameof(CanPlayPause), nameof(HasTimeline),
            nameof(ProgressFraction), nameof(ElapsedText), nameof(DurationText),
        })
        {
            OnPropertyChanged(name);
        }
        // The art is only announced when it really changed, so the view does not decode it again on every play/pause.
        var art = ArtBytes;
        if (!ReferenceEquals(art, _shownArt) && !(art is not null && _shownArt is not null && art.AsSpan().SequenceEqual(_shownArt)))
        {
            _shownArt = art;
            OnPropertyChanged(nameof(ArtBytes));
        }
        PlayPauseCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
    }

    // The Space shortcut works on whatever session the system has, whether or not the widget is on the dashboard.
    public bool CanToggleFromShortcut =>
        _service.Current is { } info && (info.Controls.HasFlag(MediaControls.Play) || info.Controls.HasFlag(MediaControls.Pause));

    public Task ToggleFromShortcut() => CanToggleFromShortcut ? Run(_service.TogglePlayPauseAsync) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanPlayPause))]
    private Task PlayPause() => Run(IsPlaying ? _service.PauseAsync : _service.PlayAsync);

    [RelayCommand(CanExecute = nameof(CanNext))]
    private Task Next() => Run(_service.NextAsync);

    [RelayCommand(CanExecute = nameof(CanPrevious))]
    private Task Previous() => Run(_service.PreviousAsync);

    // A source that refuses a command is ignored; the next change event shows what really happened.
    private static async Task Run(Func<Task> command)
    {
        try
        {
            await command();
        }
        catch (Exception)
        {
        }
    }
}
