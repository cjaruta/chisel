namespace Chisel.Core.Media;

// Turns mono audio samples into a handful of smoothed bar heights (0..1), low pitches first.
// Push feeds samples (from any thread); Update, called about 30 times a second, runs a windowed FFT over the latest
// samples, groups the bins into log-spaced bands, maps them to decibels and eases the bars up quickly and down slowly.
// Nothing is allocated after construction, and no audio is kept beyond the last FftSize samples.
public sealed class SpectrumProcessor
{
    public const int FftSize = 2048;
    public const double LowestHz = 40;
    public const double HighestHz = 16000;

    // The decibel range shown: at or below the floor is an empty bar, at or above the ceiling a full one.
    private const double FloorDb = -65;
    private const double CeilingDb = -5;
    // Time constants (seconds): bars jump up fast and fall back slowly.
    private const double AttackSeconds = 0.04;
    private const double DecaySeconds = 0.30;
    private const float Snap = 0.004f;

    private readonly object _gate = new();
    private readonly float[] _ring = new float[FftSize];
    private int _position;

    private readonly float[] _window = new float[FftSize];
    private readonly float[] _cos = new float[FftSize / 2];
    private readonly float[] _sin = new float[FftSize / 2];
    private readonly int[] _reverse = new int[FftSize];
    private readonly float[] _re = new float[FftSize];
    private readonly float[] _im = new float[FftSize];
    private readonly int[] _firstBin;
    private readonly int[] _lastBin;
    private readonly float[] _levels;
    private readonly float _scale;

    public SpectrumProcessor(int sampleRate, int bandCount = 20)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8000);
        ArgumentOutOfRangeException.ThrowIfLessThan(bandCount, 1);
        BandCount = bandCount;
        _levels = new float[bandCount];
        _firstBin = new int[bandCount];
        _lastBin = new int[bandCount];

        double windowSum = 0;
        for (var i = 0; i < FftSize; i++)
        {
            _window[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (FftSize - 1)));
            windowSum += _window[i];
        }
        // A full-scale sine peaks at windowSum / 2 in its bin; dividing by that makes 1.0 mean 0 dB.
        _scale = (float)(2 / windowSum);

        for (var i = 0; i < FftSize / 2; i++)
        {
            _cos[i] = (float)Math.Cos(2 * Math.PI * i / FftSize);
            _sin[i] = (float)-Math.Sin(2 * Math.PI * i / FftSize);
        }
        var bits = (int)Math.Log2(FftSize);
        for (var i = 0; i < FftSize; i++)
        {
            var r = 0;
            for (var b = 0; b < bits; b++)
            {
                r |= ((i >> b) & 1) << (bits - 1 - b);
            }
            _reverse[i] = r;
        }

        var top = Math.Min(HighestHz, sampleRate / 2 * 0.95);
        var binWidth = (double)sampleRate / FftSize;
        var lastUsable = FftSize / 2 - 1;
        for (var band = 0; band < bandCount; band++)
        {
            var from = LowestHz * Math.Pow(top / LowestHz, (double)band / bandCount);
            var to = LowestHz * Math.Pow(top / LowestHz, (double)(band + 1) / bandCount);
            var first = Math.Clamp((int)Math.Round(from / binWidth), 1, lastUsable);
            var last = Math.Clamp((int)Math.Round(to / binWidth) - 1, first, lastUsable);
            _firstBin[band] = first;
            _lastBin[band] = last;
        }
    }

    public int BandCount { get; }

    // Adds mono samples (-1..1). Only the newest FftSize are kept.
    public void Push(ReadOnlySpan<float> samples)
    {
        lock (_gate)
        {
            foreach (var sample in samples)
            {
                _ring[_position] = sample;
                _position = (_position + 1) % FftSize;
            }
        }
    }

    // Forgets the samples held, so the bars fall to flat (used when the system stops delivering audio).
    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_ring);
        }
    }

    // Moves the bars toward what the latest samples show; elapsedSeconds is the time since the last call.
    public void Update(double elapsedSeconds)
    {
        lock (_gate)
        {
            for (var i = 0; i < FftSize; i++)
            {
                _re[i] = _ring[(_position + i) % FftSize];
            }
        }
        for (var i = 0; i < FftSize; i++)
        {
            _re[i] *= _window[i];
            _im[i] = 0;
        }
        Transform();

        var attack = (float)(1 - Math.Exp(-elapsedSeconds / AttackSeconds));
        var decay = (float)(1 - Math.Exp(-elapsedSeconds / DecaySeconds));
        for (var band = 0; band < BandCount; band++)
        {
            float peak = 0;
            for (var bin = _firstBin[band]; bin <= _lastBin[band]; bin++)
            {
                var magnitude = MathF.Sqrt(_re[bin] * _re[bin] + _im[bin] * _im[bin]) * _scale;
                if (magnitude > peak)
                {
                    peak = magnitude;
                }
            }
            var target = peak <= 0 ? 0f : (float)Math.Clamp((20 * Math.Log10(peak) - FloorDb) / (CeilingDb - FloorDb), 0, 1);
            var level = _levels[band];
            level += (target - level) * (target > level ? attack : decay);
            _levels[band] = level < Snap && target == 0 ? 0f : level;
        }
    }

    public void CopyLevels(Span<float> destination) => _levels.AsSpan().CopyTo(destination);

    // In-place radix-2 FFT of _re / _im.
    private void Transform()
    {
        for (var i = 0; i < FftSize; i++)
        {
            var j = _reverse[i];
            if (j > i)
            {
                (_re[i], _re[j]) = (_re[j], _re[i]);
            }
        }
        for (var size = 2; size <= FftSize; size <<= 1)
        {
            var half = size / 2;
            var step = FftSize / size;
            for (var start = 0; start < FftSize; start += size)
            {
                for (var k = 0; k < half; k++)
                {
                    var c = _cos[k * step];
                    var s = _sin[k * step];
                    var a = start + k;
                    var b = a + half;
                    var tr = _re[b] * c - _im[b] * s;
                    var ti = _re[b] * s + _im[b] * c;
                    _re[b] = _re[a] - tr;
                    _im[b] = _im[a] - ti;
                    _re[a] += tr;
                    _im[a] += ti;
                }
            }
        }
    }
}
