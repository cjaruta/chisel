using System;
using System.Threading;

namespace Chisel.App.Startup;

// Saves the latest value once changes stop for a moment (not on every pixel of a drag), and at once on Flush (closing).
public sealed class DebouncedSaver<T> : IDisposable where T : class
{
    private readonly TimeProvider _time;
    private readonly TimeSpan _delay;
    private readonly Action<T> _save;
    private readonly object _gate = new();
    private ITimer? _timer;
    private T? _pending;

    public DebouncedSaver(TimeProvider time, TimeSpan delay, Action<T> save)
    {
        _time = time;
        _delay = delay;
        _save = save;
    }

    public void Note(T value)
    {
        lock (_gate)
        {
            _pending = value;
            if (_timer is null)
            {
                _timer = _time.CreateTimer(_ => Flush(), null, _delay, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _timer.Change(_delay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    public void Flush()
    {
        T? value;
        lock (_gate)
        {
            value = _pending;
            _pending = null;
        }
        if (value is not null)
        {
            _save(value);
        }
    }

    public void Dispose() => _timer?.Dispose();
}
