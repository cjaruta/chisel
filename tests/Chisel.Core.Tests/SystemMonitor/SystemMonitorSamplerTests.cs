using Chisel.Core.SystemMonitor;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.Core.Tests.SystemMonitor;

public class SystemMonitorSamplerTests
{
    private const ulong GB = 1024UL * 1024 * 1024;

    // Raw counters the test sets before each tick; Reads counts how often the sampler asked.
    private sealed class FakeSource : ISystemStatsSource
    {
        public RawSystemStats Next;
        public int Reads;
        public bool Disposed;

        public void Read(ref RawSystemStats stats)
        {
            Reads++;
            stats = Next;
        }

        public void Dispose() => Disposed = true;
    }

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly FakeSource _source = new();

    private SystemMonitorSampler Sampler() => new(_source, _time);

    private static RawSystemStats Everything(ulong idle, ulong total, ulong received, ulong sent) => new()
    {
        HasCpu = true, CpuIdle = idle, CpuTotal = total,
        HasMemory = true, MemoryTotal = 16 * GB, MemoryAvailable = 4 * GB,
        HasDisk = true, DiskTotal = 500 * GB, DiskFree = 100 * GB,
        HasNetwork = true, NetworkReceived = received, NetworkSent = sent,
        HasBattery = true, BatteryPercent = 80, Charging = true,
    };

    [Fact]
    public void DoesNotRun_UntilSomeoneWatches_AndStopsWhenTheLastOneLeaves()
    {
        using var sampler = Sampler();
        object a = new(), b = new();

        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.False(sampler.IsRunning);
        Assert.Equal(0, _source.Reads);

        sampler.Attach(a);
        sampler.Attach(b);
        sampler.Attach(a);
        Assert.True(sampler.IsRunning);
        Assert.Equal(1, _source.Reads); // one reading straight away, so the widget is not empty for a second

        _time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(4, _source.Reads);

        sampler.Detach(a);
        Assert.True(sampler.IsRunning);
        sampler.Detach(b);
        Assert.False(sampler.IsRunning);

        _time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(4, _source.Reads);
    }

    [Fact]
    public void Interval_ChangesHowOftenItSamples()
    {
        using var sampler = Sampler();
        sampler.Attach(this);
        sampler.Interval = TimeSpan.FromSeconds(5);

        _time.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(1, _source.Reads);
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, _source.Reads);
    }

    [Fact]
    public void Sample_WorksOutPercentagesAndRatesFromTheCounters()
    {
        using var sampler = Sampler();
        _source.Next = Everything(idle: 1000, total: 2000, received: 10_000, sent: 1_000);
        sampler.Attach(this);
        _source.Next = Everything(idle: 1075, total: 2100, received: 1_260_000, sent: 2_250);
        _time.Advance(TimeSpan.FromSeconds(1));

        var now = sampler.Current;
        Assert.Equal(25, now.CpuPercent!.Value, 3);
        Assert.Equal(75, now.RamPercent!.Value, 3);
        Assert.Equal(12 * GB, now.RamUsed);
        Assert.Equal(80, now.DiskPercent!.Value, 3);
        Assert.Equal(1_250_000, now.DownBytesPerSecond!.Value, 3);
        Assert.Equal(1_250, now.UpBytesPerSecond!.Value, 3);
        Assert.Equal(80, now.BatteryPercent);
        Assert.True(now.Charging);
    }

    [Fact]
    public void FirstReading_ShowsZeroForCpuAndNetwork_UntilThereIsADifference()
    {
        using var sampler = Sampler();
        _source.Next = Everything(1000, 2000, 10_000, 1_000);
        sampler.Attach(this);

        Assert.Equal(0, sampler.Current.CpuPercent);
        Assert.Equal(0, sampler.Current.DownBytesPerSecond);
    }

    [Fact]
    public void UnavailableStats_AreNull_AndNotShownAsAvailable()
    {
        using var sampler = Sampler();
        _source.Next = new RawSystemStats { HasCpu = true, CpuIdle = 1, CpuTotal = 2 };
        sampler.Attach(this);

        var now = sampler.Current;
        Assert.True(now.Has(SystemStat.Cpu));
        Assert.False(now.Has(SystemStat.Ram));
        Assert.False(now.Has(SystemStat.Disk));
        Assert.False(now.Has(SystemStat.Network));
        Assert.False(now.Has(SystemStat.Battery));
        Assert.Null(now.RamPercent);
        Assert.Null(now.BatteryPercent);
    }

    [Fact]
    public void ACounterThatGoesBackwards_GivesZero_NotANegativeOrHugeRate()
    {
        using var sampler = Sampler();
        _source.Next = Everything(1000, 2000, 10_000, 5_000);
        sampler.Attach(this);
        _source.Next = Everything(900, 1900, 2_000, 1_000);
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(0, sampler.Current.CpuPercent);
        Assert.Equal(0, sampler.Current.DownBytesPerSecond);
        Assert.Equal(0, sampler.Current.UpBytesPerSecond);
    }

    [Fact]
    public void History_KeepsEachSample_AndIsClearedWhenStopped()
    {
        using var sampler = Sampler();
        _source.Next = Everything(1000, 2000, 0, 0);
        sampler.Attach(this);
        _source.Next = Everything(1050, 2100, 0, 0);
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(new double[] { 0, 50 }, new[] { sampler.History(SystemStat.Cpu)[0], sampler.History(SystemStat.Cpu)[1] });
        Assert.Equal(2, sampler.History(SystemStat.Ram).Count);
        Assert.Equal(SystemMonitorSampler.HistoryCapacity, sampler.History(SystemStat.Cpu).Capacity);

        sampler.Detach(this);
        Assert.Equal(0, sampler.History(SystemStat.Cpu).Count);
    }

    [Fact]
    public void Sampled_IsRaisedAfterEachReading()
    {
        using var sampler = Sampler();
        var raised = 0;
        sampler.Sampled += (_, _) => raised++;
        sampler.Attach(this);
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(3, raised);
    }

    [Fact]
    public void Ticks_GoThroughTheDispatcher()
    {
        var queued = new List<Action>();
        using var sampler = new SystemMonitorSampler(_source, _time, queued.Add);
        sampler.Attach(this);
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(1, _source.Reads);
        Assert.Single(queued);
        queued[0]();
        Assert.Equal(2, _source.Reads);
    }

    [Fact]
    public void AQueuedTick_AfterStopping_DoesNothing()
    {
        var queued = new List<Action>();
        using var sampler = new SystemMonitorSampler(_source, _time, queued.Add);
        sampler.Attach(this);
        _time.Advance(TimeSpan.FromSeconds(1));
        sampler.Detach(this);

        queued[0]();
        Assert.Equal(1, _source.Reads);
    }

    [Fact]
    public void Dispose_StopsAndDisposesTheSource()
    {
        var sampler = Sampler();
        sampler.Attach(this);
        sampler.Dispose();

        Assert.False(sampler.IsRunning);
        Assert.True(_source.Disposed);
        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(1, _source.Reads);
    }

    [Fact]
    public void Sampling_DoesNotAllocate()
    {
        using var sampler = Sampler();
        _source.Next = Everything(1000, 2000, 0, 0);
        sampler.Attach(this);
        for (var i = 0; i < 100; i++)
        {
            sampler.Sample();
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            sampler.Sample();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    // A day of one-second samples: the history stays at its fixed size and the steady state allocates nothing per tick,
    // so memory does not grow over a long run.
    [Fact]
    public void ADayOfTicks_KeepsHistoryBounded_AndAllocatesNothingPerTick()
    {
        using var sampler = Sampler();
        sampler.Interval = TimeSpan.FromSeconds(1);
        sampler.Attach(new object());
        ulong idle = 0, total = 0, received = 0;
        void Step()
        {
            idle += 50; total += 100; received += 1000;
            _source.Next = Everything(idle, total, received, received);
            _time.Advance(TimeSpan.FromSeconds(1));
        }
        for (var i = 0; i < 1000; i++)
        {
            Step();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 86_400; i++)
        {
            Step();
        }
        var perTick = (GC.GetAllocatedBytesForCurrentThread() - before) / 86_400.0;

        Assert.All(Enum.GetValues<SystemStat>(), s => Assert.Equal(SystemMonitorSampler.HistoryCapacity, sampler.History(s).Count));
        Assert.True(perTick < 64, $"{perTick:F1} bytes per tick");
    }
}
