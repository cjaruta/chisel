namespace Chisel.Core.SystemMonitor;

// A fixed-size history of readings for a sparkline. The buffer is made once; adding overwrites the oldest value, so a running
// monitor never allocates. Index 0 is the oldest kept value.
public sealed class HistoryRing
{
    private readonly double[] _values;
    private int _start;

    public HistoryRing(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _values = new double[capacity];
    }

    public int Capacity => _values.Length;

    public int Count { get; private set; }

    public double this[int index] => _values[(_start + index) % _values.Length];

    public double Latest => Count == 0 ? 0 : this[Count - 1];

    public void Add(double value)
    {
        if (Count < _values.Length)
        {
            _values[(_start + Count) % _values.Length] = value;
            Count++;
        }
        else
        {
            _values[_start] = value;
            _start = (_start + 1) % _values.Length;
        }
    }

    // The largest of the newest `last` values (all of them when there are fewer); 0 when empty.
    public double MaxOfLast(int last)
    {
        var max = 0d;
        for (var i = Math.Max(0, Count - last); i < Count; i++)
        {
            max = Math.Max(max, this[i]);
        }
        return max;
    }

    public void Clear()
    {
        Count = 0;
        _start = 0;
    }
}
