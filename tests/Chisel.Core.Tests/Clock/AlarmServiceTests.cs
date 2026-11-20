using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.Core.Tests.Clock;

public class AlarmServiceTests
{
    private sealed class MemoryAlarms : IAlarmRepository
    {
        public List<Alarm> Items { get; } = [];
        public IReadOnlyList<Alarm> GetAll() => Items.ToList();
        public void Save(Alarm alarm)
        {
            var i = Items.FindIndex(a => a.Id == alarm.Id);
            if (i < 0) Items.Add(alarm); else Items[i] = alarm;
        }
        public void Delete(Guid id) => Items.RemoveAll(a => a.Id == id);
    }

    // Saturday 2000-01-01 at the given time.
    private static (AlarmService Service, FakeTimeProvider Time, MemoryAlarms Repo, List<Alarm> Fired) Make(int hour = 7, int minute = 0)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, hour, minute, 0, TimeSpan.Zero));
        var repo = new MemoryAlarms();
        var service = new AlarmService(repo, time);
        var fired = new List<Alarm>();
        service.Fired += fired.Add;
        return (service, time, repo, fired);
    }

    private static readonly TimeOnly Eight = new(8, 0);

    [Fact]
    public void AnAlarm_FiresWhenItsTimeComes_AndOnlyOnce()
    {
        var (service, time, _, fired) = Make();
        service.Add(Eight, "Wake up", AlarmDays.Every);

        time.Advance(TimeSpan.FromMinutes(59));
        service.Check();
        Assert.Empty(fired);

        time.Advance(TimeSpan.FromMinutes(1));
        service.Check();
        service.Check();
        Assert.Equal("Wake up", Assert.Single(fired).Label);
    }

    [Fact]
    public void AnAlarmWhoseTimePassedBeforeTheServiceStarted_DoesNotFire()
    {
        var (service, time, repo, fired) = Make(hour: 9);
        repo.Save(Alarm.Create(Eight, "Early", AlarmDays.Every));
        var late = new AlarmService(repo, time);
        late.Fired += fired.Add;

        time.Advance(TimeSpan.FromMinutes(5));
        late.Check();

        Assert.Empty(fired);
    }

    [Fact]
    public void ARepeatingAlarm_FiresAgainOnTheNextMatchingDay_AndSkipsOthers()
    {
        var (service, time, _, fired) = Make();
        service.Add(Eight, "Weekdays", AlarmDays.Weekdays);

        time.Advance(TimeSpan.FromHours(1)); // Saturday 08:00: not a weekday
        service.Check();
        Assert.Empty(fired);

        time.Advance(TimeSpan.FromDays(2)); // Monday 08:00
        service.Check();
        Assert.Single(fired);

        time.Advance(TimeSpan.FromDays(1)); // Tuesday 08:00
        service.Check();
        Assert.Equal(2, fired.Count);
    }

    [Fact]
    public void AOneTimeAlarm_SwitchesItselfOffAfterFiring()
    {
        var (service, time, repo, fired) = Make();
        service.Add(Eight, "Once", AlarmDays.None);

        time.Advance(TimeSpan.FromHours(1));
        service.Check();

        Assert.Single(fired);
        Assert.False(repo.Items.Single().Enabled);
        time.Advance(TimeSpan.FromDays(1));
        service.Check();
        Assert.Single(fired);
    }

    [Fact]
    public void ADisabledAlarm_NeverFires()
    {
        var (service, time, _, fired) = Make();
        var alarm = service.Add(Eight, "Off", AlarmDays.Every);
        service.SetEnabled(alarm.Id, false);

        time.Advance(TimeSpan.FromHours(1));
        service.Check();

        Assert.Empty(fired);
    }

    [Fact]
    public void AfterASleepWithinTheGrace_TheAlarmStillFiresOnce_ButAStaleOneIsSkipped()
    {
        var (service, time, _, fired) = Make();
        service.Add(Eight, "Soon", AlarmDays.Every);
        time.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(5)); // woke up at 08:05
        service.Check();
        Assert.Single(fired);

        var stale = Make();
        stale.Service.Add(Eight, "Stale", AlarmDays.Every);
        stale.Time.Advance(TimeSpan.FromHours(4)); // woke up at 11:00
        stale.Service.Check();
        Assert.Empty(stale.Fired);
    }

    [Fact]
    public void ChangingAnAlarm_AllowsItToFireAgainToday()
    {
        var (service, time, _, fired) = Make();
        var alarm = service.Add(Eight, "A", AlarmDays.Every);
        time.Advance(TimeSpan.FromHours(1));
        service.Check();
        Assert.Single(fired);

        service.Update(alarm with { Time = new TimeOnly(9, 0) });
        time.Advance(TimeSpan.FromHours(1));
        service.Check();

        Assert.Equal(2, fired.Count);
    }

    [Fact]
    public void Delete_RemovesTheAlarm()
    {
        var (service, time, repo, fired) = Make();
        var alarm = service.Add(Eight, "Gone", AlarmDays.Every);
        service.Delete(alarm.Id);

        time.Advance(TimeSpan.FromHours(1));
        service.Check();

        Assert.Empty(fired);
        Assert.Empty(repo.Items);
        Assert.Empty(service.Alarms);
    }

    [Fact]
    public void Alarms_AreListedByTimeOfDay()
    {
        var (service, _, _, _) = Make();
        service.Add(new TimeOnly(9, 0), "Late", AlarmDays.None);
        service.Add(new TimeOnly(6, 30), "Early", AlarmDays.None);
        Assert.Equal(["Early", "Late"], service.Alarms.Select(a => a.Label));
    }

    [Theory]
    [InlineData(AlarmDays.None, "Once")]
    [InlineData(AlarmDays.Every, "Every day")]
    [InlineData(AlarmDays.Weekdays, "Weekdays")]
    [InlineData(AlarmDays.Weekend, "Weekends")]
    [InlineData(AlarmDays.Monday | AlarmDays.Wednesday, "Mon, Wed")]
    public void DaysText_Summarizes(AlarmDays days, string expected) => Assert.Equal(expected, AlarmFormat.Days(days));
}
