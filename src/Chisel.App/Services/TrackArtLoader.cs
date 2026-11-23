using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Chisel.App.Services;

// What makes one track different from the next. Album art is only ever shown for the identity it was loaded for.
public sealed record TrackIdentity(string Source, string Title, string Artist, string Album);

// Loads album art for the current track and ties it to that track. Some players (Apple Music) announce a new track
// before the new thumbnail is ready, so a read at that moment gives nothing or the previous track's art. So:
// a track change clears the art at once and cancels any load still running; the load is retried a few times when it
// gives nothing (or the same picture as the previous track's, which is only believable inside one album); a result
// for a track that is no longer current is dropped; and later property events for the same track look again.
public sealed class TrackArtLoader : IDisposable
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(2000),
    ];

    private const int MaxBytes = 16 * 1024 * 1024;

    private readonly object _gate = new();
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private TrackIdentity? _track;
    private byte[]? _art;
    private byte[]? _previousArt;
    private string? _previousAlbum;
    private CancellationTokenSource? _cts;
    private bool _loading;
    private bool _gaveUp;
    private bool _disposed;

    public TrackArtLoader(Func<TimeSpan, CancellationToken, Task>? delay = null) =>
        _delay = delay ?? Task.Delay;

    // Raised (on a thread-pool thread) when art arrives for the current track.
    public event EventHandler? ArtChanged;

    // The art for this track, or null when it is not loaded (yet), or it belongs to another track.
    public byte[]? ArtFor(TrackIdentity track)
    {
        lock (_gate)
        {
            return track == _track ? _art : null;
        }
    }

    // Call on every session event. propertiesChanged is true for media-properties events, the ones that can carry new art.
    public void Observe(TrackIdentity track, Func<CancellationToken, Task<byte[]?>> load, bool propertiesChanged)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            if (track != _track)
            {
                _cts?.Cancel();
                if (_art is not null && _track is not null)
                {
                    _previousArt = _art;
                    _previousAlbum = _track.Album;
                }
                _track = track;
                _art = null;
                _gaveUp = false;
                Start(track, load, retry: true);
            }
            else if (!_loading && (_art is null ? !_gaveUp || propertiesChanged : propertiesChanged))
            {
                Start(track, load, retry: _art is null && !_gaveUp);
            }
        }
    }

    // Call with the lock held.
    private void Start(TrackIdentity track, Func<CancellationToken, Task<byte[]?>> load, bool retry)
    {
        var cts = new CancellationTokenSource();
        _cts = cts;
        _loading = true;
        _ = Task.Run(() => RunAsync(track, load, retry, cts));
    }

    private async Task RunAsync(TrackIdentity track, Func<CancellationToken, Task<byte[]?>> load, bool retry, CancellationTokenSource cts)
    {
        var ct = cts.Token;
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                byte[]? bytes = null;
                try
                {
                    bytes = await load(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception)
                {
                    // A failed read counts as "no art yet".
                }

                var arrived = false;
                lock (_gate)
                {
                    if (ct.IsCancellationRequested || track != _track)
                    {
                        return; // a stale result: the track changed while it was loading
                    }
                    if (Usable(track, bytes))
                    {
                        _art = bytes;
                        arrived = true;
                    }
                    if (arrived || !retry || attempt >= RetryDelays.Length)
                    {
                        _loading = false;
                        _gaveUp = !arrived && _art is null;
                    }
                }
                if (arrived)
                {
                    try
                    {
                        ArtChanged?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception)
                    {
                    }
                    return;
                }
                if (!retry || attempt >= RetryDelays.Length)
                {
                    return;
                }
                await _delay(RetryDelays[attempt], ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_cts, cts))
                {
                    _loading = false;
                    _cts = null; // so a later Cancel never touches a disposed source
                }
            }
            cts.Dispose();
        }
    }

    // Call with the lock held. Empty or huge data is no art. The same picture as the previous track's is only
    // accepted inside one album (otherwise it is probably the player still showing the old track's art), and a
    // re-check only counts when it really changes the picture.
    private bool Usable(TrackIdentity track, byte[]? bytes)
    {
        if (bytes is not { Length: > 0 and <= MaxBytes })
        {
            return false;
        }
        if (_art is not null)
        {
            return !bytes.AsSpan().SequenceEqual(_art);
        }
        var sameAsPrevious = _previousArt is not null && bytes.AsSpan().SequenceEqual(_previousArt);
        return !sameAsPrevious || track.Album == _previousAlbum;
    }

    // Forgets the current track and cancels any load (the loader can be used again afterwards).
    public void Clear()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _track = null;
            _art = null;
            _previousArt = null;
            _previousAlbum = null;
            _gaveUp = false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
        Clear();
    }
}
