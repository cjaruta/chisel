using Chisel.Core.Focus;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class FocusTimerRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    private FocusTimerData Load()
    {
        using var context = _fixture.CreateContext();
        return new FocusTimerRepository(context).Get();
    }

    private void Store(string json)
    {
        using var context = _fixture.CreateContext();
        context.FocusTimer.Add(new FocusTimerEntity { Version = 1, Json = json });
        context.SaveChanges();
    }

    [Fact]
    public void Empty_ReturnsTheDefaults()
    {
        Assert.Equal(FocusTimerData.Default, Load());
    }

    [Fact]
    public void Save_RoundTripsEveryField_AndReplacesThePreviousRecord()
    {
        var data = new FocusTimerData(
            new FocusSettings(50, 10, 30, 3, AutoStartNext: true, ChimeVolume: 0, FocusFace.Digital, Guid.NewGuid()),
            new FocusTimerState(FocusPhase.LongBreak, FocusRun.Running, TimeSpan.FromSeconds(95.5),
                new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.Zero), 3, 7, new DateOnly(2000, 1, 1)));
        using (var context = _fixture.CreateContext())
        {
            var repository = new FocusTimerRepository(context);
            repository.Save(FocusTimerData.Default);
            repository.Save(data);
        }

        Assert.Equal(data, Load());
        using var check = _fixture.CreateContext();
        Assert.Single(check.FocusTimer);
    }

    [Fact]
    public void BadData_FallsBackPerField_AndValuesOutOfRangeAreClamped()
    {
        Store("""{"settings":{"focus":500,"shortBreak":"x","volume":-3,"face":"Hologram","taskId":"nope"},"state":{"phase":"Nap","run":"Running","elapsedMs":-50,"sessions":2,"sessionsDay":"2000-01-01"}}""");

        var data = Load();

        Assert.Equal(FocusSettings.Default with { FocusMinutes = 180, ChimeVolume = 0 }, data.Settings);
        // A running state with no start time cannot be resumed, so it is idle; the rest is kept.
        Assert.Equal(new FocusTimerState(FocusPhase.Focus, FocusRun.Idle, TimeSpan.Zero, null, 0, 2, new DateOnly(2000, 1, 1)), data.State);
    }

    [Fact]
    public void JsonThatDoesNotParse_GivesTheDefaults()
    {
        Store("{not json");

        Assert.Equal(FocusTimerData.Default, Load());
    }

    public void Dispose() => _fixture.Dispose();
}
