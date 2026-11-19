namespace Chisel.Core.Media;

// The system's default output volume. IsAvailable is false when there is no output device (then the rest is inert).
// Changed is raised on the UI thread, also when something else (keyboard keys, the tray mixer) changes the volume.
// Commands never throw.
public interface IVolumeService
{
    bool IsAvailable { get; }
    double Level { get; }
    bool IsMuted { get; }
    event EventHandler? Changed;
    void SetLevel(double level);
    void SetMuted(bool muted);
    void ToggleMute();
}
