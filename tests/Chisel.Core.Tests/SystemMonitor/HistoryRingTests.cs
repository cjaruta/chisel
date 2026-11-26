using Chisel.Core.SystemMonitor;

namespace Chisel.Core.Tests.SystemMonitor;

public class HistoryRingTests
{
    [Fact]
    public void KeepsTheNewestValues_OldestFirst_OnceFull()
    {
        var ring = new HistoryRing(3);
        foreach (var v in new double[] { 1, 2, 3, 4, 5 })
        {
            ring.Add(v);
        }

        Assert.Equal(3, ring.Count);
        Assert.Equal(new double[] { 3, 4, 5 }, new[] { ring[0], ring[1], ring[2] });
        Assert.Equal(5, ring.Latest);
    }

    [Fact]
    public void MaxOfLast_LooksOnlyAtTheNewestValues()
    {
        var ring = new HistoryRing(5);
        foreach (var v in new double[] { 9, 1, 2, 3 })
        {
            ring.Add(v);
        }

        Assert.Equal(3, ring.MaxOfLast(3));
        Assert.Equal(9, ring.MaxOfLast(10));
    }

    [Fact]
    public void Clear_EmptiesWithoutShrinking()
    {
        var ring = new HistoryRing(4);
        ring.Add(1);
        ring.Clear();

        Assert.Equal(0, ring.Count);
        Assert.Equal(4, ring.Capacity);
        Assert.Equal(0, ring.Latest);
    }

    [Fact]
    public void Add_DoesNotAllocate()
    {
        var ring = new HistoryRing(60);
        ring.Add(1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            ring.Add(i);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
