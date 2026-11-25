using Chisel.Core.SystemMonitor;

namespace Chisel.Core.Tests.SystemMonitor;

public class SystemMonitorLayoutTests
{
    // Squishing the height keeps every stat: the rings, their strokes, the values and the labels all shrink together.
    [Fact]
    public void SquishedHeight_KeepsEveryStat_AndScalesEverythingDown()
    {
        var roomy = SystemMonitorLayout.For(645, 200, SystemStats.All, hasBattery: false);
        var squished = SystemMonitorLayout.For(645, 120, SystemStats.All, hasBattery: false);
        var flat = SystemMonitorLayout.For(645, 80, SystemStats.All, hasBattery: false);

        Assert.Equal(4, squished.Stats.Count);
        Assert.Equal(4, flat.Stats.Count);
        Assert.True(squished.Ring < roomy.Ring && flat.Ring < squished.Ring);
        Assert.True(squished.Stroke < roomy.Stroke);
        Assert.True(squished.ValueFont < roomy.ValueFont && flat.ValueFont < squished.ValueFont);
        Assert.True(squished.LabelFont < roomy.LabelFont);
        Assert.Equal(squished.Ring * SystemMonitorLayout.ValueFontFraction, squished.ValueFont, 3);
    }

    // Losing height never makes the rings bigger (as dropping to two larger rings once did).
    [Theory]
    [InlineData(645)]
    [InlineData(400)]
    [InlineData(260)]
    public void ShrinkingTheHeight_NeverGrowsTheRings(double width)
    {
        var previous = double.MaxValue;
        for (var height = 400.0; height >= 60; height -= 5)
        {
            var layout = SystemMonitorLayout.For(width, height, SystemStats.All, hasBattery: true);
            Assert.True(layout.Ring <= previous + 0.01, $"{width}x{height}: ring {layout.Ring} > {previous}");
            previous = layout.Ring;
        }
    }

    [Fact]
    public void Labels_GoBeforeTheyGetTooSmallToRead()
    {
        for (var height = 400.0; height >= 60; height -= 5)
        {
            var layout = SystemMonitorLayout.For(645, height, SystemStats.All, hasBattery: true);
            Assert.True(!layout.ShowLabels || layout.LabelFont >= SystemMonitorLayout.MinLabelFont, $"height {height}: {layout.LabelFont}");
        }
    }

    [Fact]
    public void Compact_ShowsOnlyCpuAndRam_WithoutLabelsOrSparklines()
    {
        var layout = SystemMonitorLayout.For(150, 120, SystemStats.All, hasBattery: true);

        Assert.Equal(new[] { SystemStat.Cpu, SystemStat.Ram }, layout.Stats);
        Assert.False(layout.ShowLabels);
        Assert.False(layout.ShowSparklines);
    }

    [Fact]
    public void Standard_AddsTheOtherStatsAndLabels_ButNoSparklines()
    {
        var layout = SystemMonitorLayout.For(260, 200, SystemStats.All, hasBattery: true);

        Assert.Equal(MonitorSize.Standard, layout.Size);
        Assert.Equal(new[] { SystemStat.Cpu, SystemStat.Ram, SystemStat.Disk, SystemStat.Network, SystemStat.Battery }, layout.Stats);
        Assert.True(layout.ShowLabels);
        Assert.False(layout.ShowSparklines);
    }

    [Fact]
    public void Full_AddsSparklines()
    {
        var layout = SystemMonitorLayout.For(520, 360, SystemStats.All, hasBattery: false);

        Assert.True(layout.ShowSparklines);
        Assert.DoesNotContain(SystemStat.Battery, layout.Stats);
    }

    [Fact]
    public void Stats_FollowTheChosenSet()
    {
        Assert.Equal(new[] { SystemStat.Disk, SystemStat.Network },
            SystemMonitorLayout.For(400, 300, SystemStats.Disk | SystemStats.Network, hasBattery: true).Stats);
        // Two stats fit a small widget; a tiny one with neither CPU nor RAM chosen still shows something: the first chosen stat.
        Assert.Equal(new[] { SystemStat.Disk, SystemStat.Network }, SystemMonitorLayout.For(150, 120, SystemStats.Disk | SystemStats.Network, true).Stats);
        Assert.Equal(new[] { SystemStat.Disk }, SystemMonitorLayout.For(90, 60, SystemStats.Disk | SystemStats.Network | SystemStats.Battery, true).Stats);
        Assert.Empty(SystemMonitorLayout.For(400, 300, SystemStats.None, true).Stats);
    }

    [Theory]
    [InlineData(120, 90)]
    [InlineData(160, 140)]
    [InlineData(230, 160)]
    [InlineData(300, 240)]
    [InlineData(330, 800)]
    [InlineData(900, 260)]
    [InlineData(1200, 700)]
    public void EveryGauge_FitsInsideTheWidget(double width, double height)
    {
        var layout = SystemMonitorLayout.For(width, height, SystemStats.All, hasBattery: true);

        for (var i = 0; i < layout.Stats.Count; i++)
        {
            var cell = layout.Cell(i);
            Assert.True(cell.X >= 0 && cell.Y >= 0, $"cell {i} starts outside");
            Assert.True(cell.X + cell.Width <= width + 0.01 && cell.Y + cell.Height <= height + 0.01, $"cell {i} ends outside");
            Assert.True(layout.Ring <= cell.Width + 0.01, "ring wider than its cell");
            Assert.True(layout.GaugeHeight <= cell.Height + 0.01, "gauge taller than its cell");
            Assert.True(layout.SparkWidth <= cell.Width + 0.01, "sparkline wider than its cell");
        }
    }

    [Fact]
    public void FontsAndStrokes_GrowWithTheRing()
    {
        var small = SystemMonitorLayout.For(260, 200, SystemStats.All, true);
        var large = SystemMonitorLayout.For(1000, 700, SystemStats.All, true);

        Assert.True(large.Ring > small.Ring);
        Assert.True(large.Stroke > small.Stroke);
        Assert.True(large.ValueFont > small.ValueFont);
        Assert.True(large.LabelFont >= small.LabelFont);
    }
}
