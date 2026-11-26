using System.Diagnostics;
using Chisel.App.Services;
using Chisel.Core.SystemMonitor;

namespace Chisel.App.Tests.Services;

// Reads this machine's real stats. Values vary by machine, so these check that each reading is sane, not what it is.
public class WindowsSystemStatsTests(ITestOutputHelper output)
{
    [Fact]
    public void Read_GivesSaneCpuMemoryAndDisk_WithoutAdminRights()
    {
        using var source = new WindowsSystemStats();
        var raw = default(RawSystemStats);
        source.Read(ref raw);

        Assert.True(raw.HasCpu);
        Assert.True(raw.CpuTotal >= raw.CpuIdle && raw.CpuTotal > 0);
        Assert.True(raw.HasMemory);
        Assert.InRange(raw.MemoryAvailable, 1UL, raw.MemoryTotal);
        Assert.True(raw.HasDisk);
        Assert.InRange(raw.DiskFree, 0UL, raw.DiskTotal);
        Assert.True(raw.HasNetwork);
        if (raw.HasBattery)
        {
            Assert.InRange(raw.BatteryPercent, 0, 100);
        }
    }

    // The network table is read straight from native memory, so check the row layout against the framework's own view: the
    // interfaces it counts must exist there, and its byte counters must not exceed what all up interfaces report together.
    [Fact]
    public void NetworkTotals_AreWithinWhatTheFrameworkReports()
    {
        using var source = new WindowsSystemStats();
        var raw = default(RawSystemStats);
        source.Read(ref raw);

        var all = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
            .Select(n => n.GetIPStatistics())
            .Aggregate((Received: 0L, Sent: 0L), (sum, s) => (sum.Received + s.BytesReceived, sum.Sent + s.BytesSent));
        Assert.True(raw.NetworkReceived <= (ulong)all.Received + 50_000_000, $"{raw.NetworkReceived} vs {all.Received}");
        Assert.True(raw.NetworkSent <= (ulong)all.Sent + 50_000_000, $"{raw.NetworkSent} vs {all.Sent}");

        // And a machine whose Ethernet or Wi-Fi adapter has moved data reports some of it (a wrong offset would read zero). The
        // framework also lists filter-driver copies of each adapter with the same counters; those are what the source skips.
        var physical = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                && n.NetworkInterfaceType is System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 or System.Net.NetworkInformation.NetworkInterfaceType.Ethernet
                && !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) && !n.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase))
            .Sum(n => n.GetIPStatistics().BytesReceived);
        output.WriteLine($"received: ours {raw.NetworkReceived}, physical {physical}, all up {all.Received}");
        if (physical > 0)
        {
            Assert.True(raw.NetworkReceived > 0);
        }
    }

    [Fact]
    public void Read_DoesNotAllocate_AndIsCheap()
    {
        using var source = new WindowsSystemStats();
        using var sampler = new SystemMonitorSampler(source, TimeProvider.System);
        for (var i = 0; i < 20; i++)
        {
            sampler.Sample();
        }

        const int samples = 200;
        var cpu = Process.GetCurrentProcess().TotalProcessorTime;
        var clock = Stopwatch.StartNew();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < samples; i++)
        {
            sampler.Sample();
        }
        var allocated = (GC.GetAllocatedBytesForCurrentThread() - before) / samples;
        var wall = clock.Elapsed.TotalMilliseconds / samples;
        var cpuPerSample = (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMilliseconds / samples;
        output.WriteLine($"per sample: {wall * 1000:0} us wall, {cpuPerSample * 1000:0} us CPU, {allocated} B allocated");

        Assert.True(allocated <= 64, $"{allocated} B per sample");
        Assert.True(wall < 5, $"{wall:0.00} ms per sample");
    }
}
