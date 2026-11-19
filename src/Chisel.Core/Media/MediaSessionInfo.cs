namespace Chisel.Core.Media;

public enum MediaPlaybackState { Stopped, Paused, Playing }

[Flags]
public enum MediaControls { None = 0, Play = 1, Pause = 2, Next = 4, Previous = 8 }

// What is playing on this PC right now. Anything the source does not report is empty (text) or null (times, art).
public sealed record MediaSessionInfo
{
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string AlbumTitle { get; init; } = "";
    public string AlbumArtist { get; init; } = "";
    public int TrackNumber { get; init; }
    // The app id the session came from (for example "Spotify.exe"); see MediaFormat.SourceName.
    public string SourceApp { get; init; } = "";
    public MediaPlaybackState State { get; init; } = MediaPlaybackState.Stopped;
    public MediaControls Controls { get; init; }
    // Where playback is as of when this info was read, and how long the track is. Null when the source gives none.
    public TimeSpan? Position { get; init; }
    public TimeSpan? Duration { get; init; }
    public byte[]? AlbumArt { get; init; }
}
