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
        var settings = new UserSettings("Caden", new TimeOnly(6, 30), new TimeOnly(23, 0));

        var entity = settings.ToEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("Caden", entity.Name);
        Assert.Equal(new TimeOnly(6, 30), entity.WakeTime);
        Assert.Equal(new TimeOnly(23, 0), entity.BedTime);
    }

    [Fact]
    public void ToDomain_CopiesNameWakeTimeAndBedTime()
    {
        var entity = new UserSettingsEntity
        {
            Id = Guid.NewGuid(),
            Name = "Caden",
            WakeTime = new TimeOnly(6, 30),
            BedTime = new TimeOnly(23, 0)
        };

        var settings = entity.ToDomain();

        Assert.Equal("Caden", settings.Name);
        Assert.Equal(new TimeOnly(6, 30), settings.WakeTime);
        Assert.Equal(new TimeOnly(23, 0), settings.BedTime);
    }
}