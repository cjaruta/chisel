using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Chisel.Core.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Chisel.App.Services;

// The real media service: the Windows system media session (the one the volume flyout shows), so no other app of
// ours has to be open. It listens only while something is subscribed to Changed, re-reads the current session on
// every session / properties / playback / timeline event, and raises Changed on the UI thread. Any API failure is
// swallowed: a bad session just reads as "nothing playing".
public sealed class WindowsMediaService : IMediaService, IDisposable
{
    private readonly object _gate = new();
    private int _subscribers;
    private EventHandler? _changed;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private MediaSessionInfo? _current;
    private int _version;
    private bool _disposed;
    private string? _artKey;
    private byte[]? _art;

    public MediaSessionInfo? Current => Volatile.Read(ref _current);

    public event EventHandler? Changed
    {
        add
        {
            lock (_gate)
            {
                _changed += value;
                if (++_subscribers == 1)
                {
                    _ = StartAsync();
                }
            }
        }
        remove
        {
            lock (_gate)
            {
                _changed -= value;
                if (--_subscribers == 0)
                {
                    Stop();
                }
            }
        }
    }

    private async Task StartAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            lock (_gate)
            {
                if (_disposed || _subscribers == 0)
                {
                    return;
                }
                _manager = manager;
                manager.CurrentSessionChanged += OnSessionChanged;
                Attach(manager.GetCurrentSession());
            }
            await RefreshAsync();
        }
        catch (Exception)
        {
            Publish(null, Interlocked.Increment(ref _version));
        }
    }

    // Call with the lock held.
    private void Stop()
    {
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= OnSessionChanged;
            _manager = null;
        }
        Attach(null);
        _art = null;
        _artKey = null;
        Interlocked.Increment(ref _version);
        Interlocked.Exchange(ref _current, null);
    }

    // Moves the event subscriptions from the old session to the new one. Call with the lock held.
    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_session is { } old)
        {
            old.MediaPropertiesChanged -= OnSessionEvent;
            old.PlaybackInfoChanged -= OnSessionEvent;
            old.TimelinePropertiesChanged -= OnSessionEvent;
        }
        _session = session;
        if (session is not null)
        {
            session.MediaPropertiesChanged += OnSessionEvent;
            session.PlaybackInfoChanged += OnSessionEvent;
            session.TimelinePropertiesChanged += OnSessionEvent;
        }
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, object args)
    {
        lock (_gate)
        {
            if (_disposed || _subscribers == 0)
            {
                return;
            }
            try
            {
                Attach(sender.GetCurrentSession());
            }
            catch (Exception)
            {
                Attach(null);
            }
        }
        _ = RefreshAsync();
    }

    private void OnSessionEvent(GlobalSystemMediaTransportControlsSession sender, object args) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        var version = Interlocked.Increment(ref _version);
        MediaSessionInfo? info = null;
        try
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_gate)
            {
                session = _session;
            }
            if (session is not null)
            {
                info = await ReadAsync(session);
            }
        }
        catch (Exception)
        {
            info = null;
        }
        Publish(info, version);
    }

    // Only the newest read is shown; an older one that finished late is dropped.
    private void Publish(MediaSessionInfo? info, int version) =>
        Dispatcher.UIThread.Post(() =>
        {
            EventHandler? handler;
            lock (_gate)
            {
                if (_disposed || _subscribers == 0 || version != Volatile.Read(ref _version))
                {
                    return;
                }
                handler = _changed;
            }
            Interlocked.Exchange(ref _current, info);
            try
            {
                handler?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
            }
        });

    private async Task<MediaSessionInfo?> ReadAsync(GlobalSystemMediaTransportControlsSession session)
    {
        var playback = session.GetPlaybackInfo();
        if (playback is null || playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed)
        {
            return null;
        }

        GlobalSystemMediaTransportControlsSessionMediaProperties? props = null;
        try
        {
            props = await session.TryGetMediaPropertiesAsync();
        }
        catch (Exception)
        {
            // Some sources fail here; show what we have (state and controls) without text.
        }

        var controls = playback.Controls;
        var state = playback.PlaybackStatus switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaPlaybackState.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaPlaybackState.Paused,
            _ => MediaPlaybackState.Stopped,
        };

        TimeSpan? position = null, duration = null;
        try
        {
            var timeline = session.GetTimelineProperties();
            var length = timeline.EndTime - timeline.StartTime;
            if (length > TimeSpan.Zero)
            {
                duration = length;
                var at = timeline.Position - timeline.StartTime;
                if (state == MediaPlaybackState.Playing)
                {
                    // The position is as of LastUpdatedTime; bring it up to now.
                    at += DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
                }
                position = at < TimeSpan.Zero ? TimeSpan.Zero : at > length ? length : at;
            }
        }
        catch (Exception)
        {
        }

        var title = props?.Title ?? "";
        var artist = props?.Artist ?? "";
        var album = props?.AlbumTitle ?? "";
        return new MediaSessionInfo
        {
            Title = title, Artist = artist, AlbumTitle = album,
            AlbumArtist = props?.AlbumArtist ?? "",
            TrackNumber = props?.TrackNumber ?? 0,
            SourceApp = session.SourceAppUserModelId ?? "",
            State = state,
            Controls = (controls.IsPlayEnabled ? MediaControls.Play : 0) | (controls.IsPauseEnabled ? MediaControls.Pause : 0)
                | (controls.IsNextEnabled ? MediaControls.Next : 0) | (controls.IsPreviousEnabled ? MediaControls.Previous : 0),
            Position = position, Duration = duration,
            AlbumArt = await ArtAsync(props, $"{session.SourceAppUserModelId}|{title}|{artist}|{album}"),
        };
    }

    // The thumbnail as bytes, read once per track (the same bytes are handed back until the track changes).
    private async Task<byte[]?> ArtAsync(GlobalSystemMediaTransportControlsSessionMediaProperties? props, string key)
    {
        if (props?.Thumbnail is not { } thumbnail)
        {
            _art = null;
            _artKey = null;
            return null;
        }
        if (key == _artKey)
        {
            return _art;
        }
        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            if (stream.Size is 0 or > 16 * 1024 * 1024)
            {
                return null;
            }
            var bytes = new byte[stream.Size];
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);
            _art = bytes;
            _artKey = key;
            return bytes;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Task PlayAsync() => Send(s => s.TryPlayAsync().AsTask());
    public Task PauseAsync() => Send(s => s.TryPauseAsync().AsTask());
    public Task TogglePlayPauseAsync() => Send(s => s.TryTogglePlayPauseAsync().AsTask());
    public Task NextAsync() => Send(s => s.TrySkipNextAsync().AsTask());
    public Task PreviousAsync() => Send(s => s.TrySkipPreviousAsync().AsTask());

    private async Task Send(Func<GlobalSystemMediaTransportControlsSession, Task> command)
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_gate)
        {
            session = _session;
        }
        if (session is null)
        {
            return;
        }
        try
        {
            await command(session);
        }
        catch (Exception)
        {
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _changed = null;
            _subscribers = 0;
            Stop();
        }
    }
}
