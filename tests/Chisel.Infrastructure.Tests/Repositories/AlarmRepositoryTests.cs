using Chisel.Core.Clock;
using Chisel.Infrastructure.Mapping;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class AlarmRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Mapper_RoundTripsEveryField()
    {
        var alarm = new Alarm(Guid.NewGuid(), new TimeOnly(6, 45), "Test alarm", false, AlarmDays.Monday | AlarmDays.Friday, new DateOnly(2000, 1, 1));
        Assert.Equal(alarm, alarm.ToEntity().ToDomain());
    }

    [Fact]
    public void Save_AddsThenReplaces_AndGetAllReturnsEveryAlarm()
    {
        var first = Alarm.Create(new TimeOnly(7, 0), "First", AlarmDays.Every);
        var second = Alarm.Create(new TimeOnly(8, 0), "Second", AlarmDays.None);
        using (var context = _fixture.CreateContext())
        {
            var repository = new AlarmRepository(context);
            repository.Save(first);
            repository.Save(second);
            repository.Save(first with { Label = "Renamed", Enabled = false });
        }

        using var again = _fixture.CreateContext();
        var all = new AlarmRepository(again).GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal("Renamed", all.Single(a => a.Id == first.Id).Label);
        Assert.False(all.Single(a => a.Id == first.Id).Enabled);
    }

    [Fact]
    public void Delete_RemovesOne_AndIgnoresAnUnknownId()
    {
        var alarm = Alarm.Create(new TimeOnly(7, 0), "Gone", AlarmDays.Every);
        using var context = _fixture.CreateContext();
        var repository = new AlarmRepository(context);
        repository.Save(alarm);
        repository.Delete(Guid.NewGuid());
        Assert.Single(repository.GetAll());
        repository.Delete(alarm.Id);
        Assert.Empty(repository.GetAll());
    }

    public void Dispose() => _fixture.Dispose();
}
