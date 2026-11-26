using Chisel.Core.SystemMonitor;

namespace Chisel.App.Tests.Support;

// Fixed system stats for widget tests: 25% CPU after the first tick (each read adds 100 ticks, 75 idle), 12 of 16 GB RAM,
// 400 of 500 GB disk, 1.25 MB/s down and 125 KB/s up. No battery unless one is given.
public sealed class FakeSystemStats : ISystemStatsSource
{
    private const ulong GB = 1024UL * 1024 * 1024;
    private ulong _reads;

    public int? Battery { get; set; }
    public bool Charging { get; set; }
    public bool HasDisk { get; set; } = true;

    public void Read(ref RawSystemStats stats)
    {
        _reads++;
        stats.HasCpu = true;
        stats.CpuTotal = _reads * 100;
        stats.CpuIdle = _reads * 75;
        stats.HasMemory = true;
        stats.MemoryTotal = 16 * GB;
        stats.MemoryAvailable = 4 * GB;
        stats.HasDisk = HasDisk;
        stats.DiskTotal = 500 * GB;
        stats.DiskFree = 100 * GB;
        stats.HasNetwork = true;
        stats.NetworkReceived = _reads * 1_250_000;
        stats.NetworkSent = _reads * 125_000;
        if (Battery is { } percent)
        {
            stats.HasBattery = true;
            stats.BatteryPercent = percent;
            stats.Charging = Charging;
        }
    }

    public void Dispose() { }
}
