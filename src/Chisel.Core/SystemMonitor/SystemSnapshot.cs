namespace Chisel.Core.SystemMonitor;

// What the operating system reports at one moment: running counters (CPU time, network bytes) and plain readings. A source sets
// the Has flag of each group it could read and leaves the rest alone. A struct the sampler fills on the stack, so reading is free.
public struct RawSystemStats
{
    public bool HasCpu;
    public ulong CpuIdle;   // idle time since boot, any unit
    public ulong CpuTotal;  // all CPU time since boot, same unit (includes idle)

    public bool HasMemory;
    public ulong MemoryTotal;
    public ulong MemoryAvailable;

    public bool HasDisk;
    public ulong DiskTotal;
    public ulong DiskFree;

    public bool HasNetwork;
    public ulong NetworkReceived; // bytes since the interfaces came up
    public ulong NetworkSent;

    public bool HasBattery;
    public int BatteryPercent;
    public bool Charging;
}

// Reads the raw stats. Implementations must not need admin rights, the network or an account, and should not allocate.
public interface ISystemStatsSource : IDisposable
{
    void Read(ref RawSystemStats stats);
}

// The latest values the widget shows: percentages, sizes and rates. A stat the source could not read is null. One instance is
// reused for the sampler's lifetime.
public sealed class SystemSnapshot
{
    public double? CpuPercent { get; internal set; }

    public double? RamPercent { get; internal set; }
    public ulong RamUsed { get; internal set; }
    public ulong RamTotal { get; internal set; }

    public double? DiskPercent { get; internal set; }
    public ulong DiskUsed { get; internal set; }
    public ulong DiskTotal { get; internal set; }

    public double? DownBytesPerSecond { get; internal set; }
    public double? UpBytesPerSecond { get; internal set; }

    public int? BatteryPercent { get; internal set; }
    public bool Charging { get; internal set; }

    public bool Has(SystemStat stat) => stat switch
    {
        SystemStat.Cpu => CpuPercent.HasValue,
        SystemStat.Ram => RamPercent.HasValue,
        SystemStat.Disk => DiskPercent.HasValue,
        SystemStat.Network => DownBytesPerSecond.HasValue,
        SystemStat.Battery => BatteryPercent.HasValue,
        _ => false,
    };
}
