namespace Chisel.Core.Media;

// The system's current media session. Current is null when nothing is playing or paused.
// Changed can be raised on any thread. Commands never throw; a source that refuses one is ignored.
public interface IMediaService
{
    MediaSessionInfo? Current { get; }
    event EventHandler? Changed;
    Task PlayAsync();
    Task PauseAsync();
    Task TogglePlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
}
