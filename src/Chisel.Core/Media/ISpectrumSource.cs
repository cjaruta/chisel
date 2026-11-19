namespace Chisel.Core.Media;

// Where the visualizer bars come from. Start and Stop turn audio capture on and off (nothing is captured while stopped);
// ReadLevels fills one 0..1 value per band, falling to flat when there is no sound or capture failed.
public interface ISpectrumSource : IDisposable
{
    int BandCount { get; }
    bool IsRunning { get; }
    void Start();
    void Stop();
    void ReadLevels(Span<float> levels, double elapsedSeconds);
}
