using System;
using Chisel.Core.Media;

namespace Chisel.App.Services;

// A spectrum source that makes its own sound: a slow bass pulse, a gliding mid tone and a quick high shimmer, run through
// the same SpectrumProcessor as real audio. Used by tests and the capture scene; it never touches the audio system.
public sealed class SyntheticSpectrumSource : ISpectrumSource
{
    private const int Rate = 44100;
    private readonly SpectrumProcessor _processor = new(Rate);
    private readonly float[] _chunk = new float[2048];
    private double _time;

    public int BandCount => _processor.BandCount;
    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public void ReadLevels(Span<float> levels, double elapsedSeconds)
    {
        if (!IsRunning)
        {
            _processor.Clear();
        }
        else
        {
            var remaining = (int)(elapsedSeconds * Rate);
            while (remaining > 0)
            {
                var count = Math.Min(remaining, _chunk.Length);
                for (var i = 0; i < count; i++)
                {
                    var t = _time + i / (double)Rate;
                    var bass = 0.35 * (0.6 + 0.4 * Math.Sin(2 * Math.PI * 2 * t)) * Math.Sin(2 * Math.PI * 90 * t);
                    var mid = 0.2 * Math.Sin(2 * Math.PI * (600 + 400 * Math.Sin(t * 0.7)) * t);
                    var high = 0.1 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * 5 * t)) * Math.Sin(2 * Math.PI * 7000 * t);
                    _chunk[i] = (float)(bass + mid + high);
                }
                _processor.Push(_chunk.AsSpan(0, count));
                _time += count / (double)Rate;
                remaining -= count;
            }
        }
        _processor.Update(elapsedSeconds);
        _processor.CopyLevels(levels);
    }

    public void Dispose() => IsRunning = false;
}
