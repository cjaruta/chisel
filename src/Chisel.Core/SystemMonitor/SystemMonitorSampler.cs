namespace Chisel.Core.SystemMonitor;

// The one sampler every system monitor widget shares. It runs only while something is attached (a widget that is on screen):
// the first Attach takes a reading at once and starts a timer, the last Detach stops and drops the timer and clears the history.
// Each tick is handed to `dispatch` (the UI thread in the app) and reads the source into a reused snapshot and history rings,
// so a running sampler allocates nothing. CPU and network come from the difference between two readings; the first shows 0.
public sealed class SystemMonitorSampler : IDisposable
{
    // About a minute of samples at the default one-second rate.
    public const int HistoryCapacity = 60;

    private readonly ISystemStatsSource _source;
    private readonly TimeProvider _time;
    private readonly Action<Action> _dispatch;
    private readonly Action _tick;
    private readonly List<object> _watchers = [];
    private readonly HistoryRing[] _history;
    private readonly HistoryRing _up = new(HistoryCapacity);
    private ITimer? _timer;
    private TimeSpan _interval = TimeSpan.FromSeconds(1);
    private RawSystemStats _previous;
    private long _previousTime;
    private bool _hasPrevious;
    private bool _disposed;

    public SystemMonitorSampler(ISystemStatsSource source, TimeProvider time, Action<Action>? dispatch = null)
    {
        _source = source;
        _time = time;
        _dispatch = dispatch ?? (action => action());
        _tick = Tick;
        _history = new HistoryRing[Enum.GetValues<SystemStat>().Length];
        for (var i = 0; i < _history.Length; i++)
        {
            _history[i] = new HistoryRing(HistoryCapacity);
        }
    }

    public SystemSnapshot Current { get; } = new();

    public bool IsRunning => _timer is not null;

    // Raised on the dispatcher after each reading.
    public event EventHandler? Sampled;

    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            if (value <= TimeSpan.Zero || value == _interval)
            {
                return;
            }
            _interval = value;
            _timer?.Change(value, value);
        }
    }

    // The recent values of a stat, oldest first: percentages, or for the network the download rate in bytes per second.
    public HistoryRing History(SystemStat stat) => _history[(int)stat];

    public HistoryRing UploadHistory => _up;

    public void Attach(object watcher)
    {
        if (_disposed || _watchers.Contains(watcher))
        {
            return;
        }
        _watchers.Add(watcher);
        if (_timer is null)
        {
            _timer = _time.CreateTimer(_ => _dispatch(_tick), null, _interval, _interval);
            Sample();
        }
    }

    public void Detach(object watcher)
    {
        if (_watchers.Remove(watcher) && _watchers.Count == 0)
        {
            Stop();
        }
    }

    private void Tick()
    {
        // A tick queued just before the last widget left is dropped.
        if (_timer is not null)
        {
            Sample();
        }
    }

    // Takes one reading now. Called by the timer; public for tests and measurements.
    public void Sample()
    {
        var raw = default(RawSystemStats);
        _source.Read(ref raw);
        var now = _time.GetTimestamp();
        var seconds = _hasPrevious ? _time.GetElapsedTime(_previousTime, now).TotalSeconds : 0;
        var s = Current;

        if (raw.HasCpu)
        {
            var total = Delta(_previous.CpuTotal, raw.CpuTotal, _previous.HasCpu);
            var idle = Delta(_previous.CpuIdle, raw.CpuIdle, _previous.HasCpu);
            s.CpuPercent = total > 0 && idle <= total ? 100.0 * (total - idle) / total : 0;
        }
        else
        {
            s.CpuPercent = null;
        }

        if (raw.HasMemory && raw.MemoryTotal > 0)
        {
            s.RamTotal = raw.MemoryTotal;
            s.RamUsed = raw.MemoryTotal - Math.Min(raw.MemoryAvailable, raw.MemoryTotal);
            s.RamPercent = 100.0 * s.RamUsed / s.RamTotal;
        }
        else
        {
            s.RamPercent = null;
        }

        if (raw.HasDisk && raw.DiskTotal > 0)
        {
            s.DiskTotal = raw.DiskTotal;
            s.DiskUsed = raw.DiskTotal - Math.Min(raw.DiskFree, raw.DiskTotal);
            s.DiskPercent = 100.0 * s.DiskUsed / s.DiskTotal;
        }
        else
        {
            s.DiskPercent = null;
        }

        if (raw.HasNetwork)
        {
            var ok = _previous.HasNetwork && seconds > 0;
            s.DownBytesPerSecond = ok ? Delta(_previous.NetworkReceived, raw.NetworkReceived, true) / seconds : 0;
            s.UpBytesPerSecond = ok ? Delta(_previous.NetworkSent, raw.NetworkSent, true) / seconds : 0;
        }
        else
        {
            s.DownBytesPerSecond = s.UpBytesPerSecond = null;
        }

        s.BatteryPercent = raw.HasBattery ? Math.Clamp(raw.BatteryPercent, 0, 100) : null;
        s.Charging = raw.HasBattery && raw.Charging;

        Record(SystemStat.Cpu, s.CpuPercent);
        Record(SystemStat.Ram, s.RamPercent);
        Record(SystemStat.Disk, s.DiskPercent);
        Record(SystemStat.Network, s.DownBytesPerSecond);
        Record(SystemStat.Battery, s.BatteryPercent);
        if (s.UpBytesPerSecond is { } up)
        {
            _up.Add(up);
        }

        _previous = raw;
        _previousTime = now;
        _hasPrevious = true;
        Sampled?.Invoke(this, EventArgs.Empty);
    }

    // How much a running counter grew; 0 for the first reading or when it went backwards (an interface reset).
    private static ulong Delta(ulong before, ulong after, bool hadBefore) => hadBefore && after >= before ? after - before : 0;

    private void Record(SystemStat stat, double? value)
    {
        if (value is { } v)
        {
            _history[(int)stat].Add(v);
        }
    }

    private void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _hasPrevious = false;
        _previous = default;
        foreach (var ring in _history)
        {
            ring.Clear();
        }
        _up.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _watchers.Clear();
        Stop();
        _source.Dispose();
    }
}
