using Chisel.Core.Schedule;
using Chisel.Infrastructure.Seeding;
using Xunit;

namespace Chisel.Infrastructure.Tests.Seeding;

public class SeedDataTests
{
    [Fact]
    public void CreateUserSettings_ReturnsTheDefaultWakeAndBedTimes()
    {
        var settings = SeedData.CreateUserSettings();

        Assert.Equal(new TimeOnly(6, 30), settings.WakeTime);
        Assert.Equal(new TimeOnly(23, 0), settings.BedTime);
    }

    [Fact]
    public void CreateSchedule_ReturnsTheDefaultBlocksInOrder()
    {
        var schedule = SeedData.CreateSchedule();

        Assert.Equal(
            new[] { "Focus", "Focus", "Workout", "Focus", "Read" },
            schedule.Blocks.Select(b => b.Title).ToArray());

        Assert.Equal(new TimeOnly(8, 0), schedule.Blocks[0].Start);
        Assert.Equal(new TimeOnly(11, 0), schedule.Blocks[0].End);
        Assert.Equal(new TimeOnly(12, 0), schedule.Blocks[1].Start);
        Assert.Equal(new TimeOnly(14, 0), schedule.Blocks[1].End);
        Assert.Equal(new TimeOnly(14, 0), schedule.Blocks[2].Start);
        Assert.Equal(new TimeOnly(15, 0), schedule.Blocks[2].End);
        Assert.Equal(new TimeOnly(16, 0), schedule.Blocks[3].Start);
        Assert.Equal(new TimeOnly(18, 0), schedule.Blocks[3].End);
        Assert.Equal(new TimeOnly(22, 0), schedule.Blocks[4].Start);
        Assert.Equal(new TimeOnly(23, 0), schedule.Blocks[4].End);
    }

    [Fact]
    public void CreateMarkers_ReturnsCoffeeWorkAndDinnerMarkers()
    {
        var markers = SeedData.CreateMarkers();

        Assert.Equal(
            new[] { "Last coffee", "All work done", "Dinner" },
            markers.Select(m => m.Label).ToArray());

        Assert.Equal(new TimeOnly(13, 0), markers[0].Time);
        Assert.Equal(MarkerKind.Reminder, markers[0].Kind);

        Assert.Equal(new TimeOnly(18, 0), markers[1].Time);
        Assert.Equal(MarkerKind.Checkpoint, markers[1].Kind);

        Assert.Equal(new TimeOnly(18, 30), markers[2].Time);
        Assert.Equal(MarkerKind.Reminder, markers[2].Kind);
    }
}
