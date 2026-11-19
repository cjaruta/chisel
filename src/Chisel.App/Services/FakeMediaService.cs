using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chisel.Core.Media;

namespace Chisel.App.Services;

// A media service that plays nothing on its own: Current is whatever it was set to. Used by tests and capture scenes,
// and as the default when no real one is given. Commands are recorded in Calls.
public sealed class FakeMediaService : IMediaService
{
    private MediaSessionInfo? _current;

    public MediaSessionInfo? Current
    {
        get => _current;
        set
        {
            _current = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public List<string> Calls { get; } = [];
    public bool Throws { get; set; }

    public event EventHandler? Changed;

    public Task PlayAsync() => Record("Play");
    public Task PauseAsync() => Record("Pause");
    public Task TogglePlayPauseAsync() => Record("Toggle");
    public Task NextAsync() => Record("Next");
    public Task PreviousAsync() => Record("Previous");

    private Task Record(string call)
    {
        Calls.Add(call);
        return Throws ? Task.FromException(new InvalidOperationException("refused")) : Task.CompletedTask;
    }

    // Sample metadata for capture scenes (fake names only).
    public static MediaSessionInfo Sample(MediaPlaybackState state = MediaPlaybackState.Playing) => new()
    {
        Title = "Sample Song", Artist = "Test Artist", AlbumTitle = "Sample Album", AlbumArtist = "Test Artist",
        TrackNumber = 3, SourceApp = "Spotify.exe", State = state,
        Controls = MediaControls.Play | MediaControls.Pause | MediaControls.Next | MediaControls.Previous,
        Position = TimeSpan.FromSeconds(83), Duration = TimeSpan.FromSeconds(215),
    };
}
