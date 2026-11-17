using Chisel.Core.Settings;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;
using Xunit;

namespace Chisel.Infrastructure.Tests.Mapping;

public class UserSettingsMapperTests
{
    [Fact]
    public void ToEntity_CopiesNameWakeTimeAndBedTime()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));

        var entity = settings.ToEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("Test User", entity.Name);
        Assert.Equal(new TimeOnly(6, 30), entity.WakeTime);
        Assert.Equal(new TimeOnly(23, 0), entity.BedTime);
    }

    [Fact]
    public void ToDomain_CopiesNameWakeTimeAndBedTime()
    {
        var entity = new UserSettingsEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            WakeTime = new TimeOnly(6, 30),
            BedTime = new TimeOnly(23, 0)
        };

        var settings = entity.ToDomain();

        Assert.Equal("Test User", settings.Name);
        Assert.Equal(new TimeOnly(6, 30), settings.WakeTime);
        Assert.Equal(new TimeOnly(23, 0), settings.BedTime);
    }

    [Fact]
    public void ToEntity_CopiesTheSchedulePalette()
    {
        var settings = new UserSettings("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), schedulePalette: "saturated");

        Assert.Equal("saturated", settings.ToEntity().SchedulePalette);
    }

    [Fact]
    public void ToDomain_CopiesTheSchedulePalette()
    {
        var entity = new UserSettingsEntity { Name = "Test User", WakeTime = new TimeOnly(6, 30), BedTime = new TimeOnly(23, 0), SchedulePalette = "saturated" };

        Assert.Equal("saturated", entity.ToDomain().SchedulePalette);
    }

    [Fact]
    public void ToDomain_EmptySchedulePalette_IsTheDefault()
    {
        var entity = new UserSettingsEntity { Name = "Test User", WakeTime = new TimeOnly(6, 30), BedTime = new TimeOnly(23, 0), SchedulePalette = "" };

        Assert.Equal(UserSettings.DefaultSchedulePalette, entity.ToDomain().SchedulePalette);
    }
}
