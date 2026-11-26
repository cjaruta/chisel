using Chisel.Core.SystemMonitor;

namespace Chisel.Core.Tests.SystemMonitor;

public class StatFormatTests
{
    private const ulong GB = 1024UL * 1024 * 1024;

    [Theory]
    [InlineData(0UL, "0 GB")]
    [InlineData(GB / 2, "0.5 GB")]
    [InlineData(16 * GB, "16 GB")]
    [InlineData(10 * GB + GB / 10 * 6, "10.6 GB")]
    [InlineData(476 * GB + GB / 3, "476 GB")]
    [InlineData(2048 * GB, "2 TB")]
    public void Bytes_InGigabytesOrTerabytes(ulong bytes, string expected) => Assert.Equal(expected, StatFormat.Bytes(bytes));

    [Fact]
    public void UsedOfTotal_SharesTheTotalsUnit()
    {
        Assert.Equal("9.9/16 GB", StatFormat.UsedOfTotal(9 * GB + GB / 10 * 9, 16 * GB));
        Assert.Equal("0.4/2 TB", StatFormat.UsedOfTotal(400 * GB, 2048 * GB));
    }

    [Theory]
    [InlineData(0, "0 Kbps")]
    [InlineData(100, "0.8 Kbps")]
    [InlineData(1_250, "10 Kbps")]
    [InlineData(1_250_000, "10 Mbps")]
    [InlineData(1_550_000, "12.4 Mbps")]
    [InlineData(150_000_000, "1.2 Gbps")]
    public void BitsPerSecond_FromBytesPerSecond(double bytesPerSecond, string expected) =>
        Assert.Equal(expected, StatFormat.BitsPerSecond(bytesPerSecond));

    [Fact]
    public void ShortRate_DropsTheUnitWordForTheRingCenter() => Assert.Equal("12.4M", StatFormat.ShortRate(1_550_000));

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(22.5, 23)]
    [InlineData(140, 100)]
    [InlineData(double.NaN, 0)]
    public void WholePercent_IsRoundedAndClamped(double value, int expected) => Assert.Equal(expected, StatFormat.WholePercent(value));

    [Fact]
    public void SpokenNames_ReadNaturally()
    {
        Assert.Equal("CPU 23 percent", StatFormat.SpokenPercent("CPU", 23));
        Assert.Equal("RAM 62 percent, 9.9 of 16 gigabytes used", StatFormat.SpokenUsage("RAM", 62, 9 * GB + GB / 10 * 9, 16 * GB));
        Assert.Equal("Network down 12.4 megabits per second, up 10 kilobits per second", StatFormat.SpokenNetwork(1_550_000, 1_250));
        Assert.Equal("Battery 80 percent, charging", StatFormat.SpokenBattery(80, charging: true));
        Assert.Equal("Battery 80 percent", StatFormat.SpokenBattery(80, charging: false));
    }
}
