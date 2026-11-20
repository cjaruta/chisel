using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class AlarmsViewModelTests
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

    private sealed class CountingSound : IAlertSound
    {
        public int Plays { get; private set; }
        public void Play() => Plays++;
    }

    private static (AlarmsViewModel Vm, AlertsViewModel Alerts, FakeTimeProvider Time, CountingSound Sound) Make()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 7, 0, 0, TimeSpan.Zero));
        var sound = new CountingSound();
        var alerts = new AlertsViewModel(sound);
        return (new AlarmsViewModel(new AlarmService(new MemoryAlarms(), time), alerts), alerts, time, sound);
    }

    [Fact]
    public void Adding_UsesTheTimeLabelAndDaysEntered_AndClearsTheForm()
    {
        var (vm, _, _, _) = Make();
        vm.NewTime = "08:30";
        vm.NewLabel = "Stand up";
        vm.Days.Single(d => d.Day == AlarmDays.Monday).IsOn = true;
        vm.Days.Single(d => d.Day == AlarmDays.Friday).IsOn = true;

        vm.AddCommand.Execute(null);

        var item = Assert.Single(vm.Items);
        Assert.Equal("08:30", item.TimeText);
        Assert.Equal("Stand up", item.Label);
        Assert.Equal("Mon, Fri", item.DaysText);
        Assert.Equal("", vm.NewLabel);
        Assert.All(vm.Days, d => Assert.False(d.IsOn));
    }

    [Theory]
    [InlineData("")]
    [InlineData("25:00")]
    [InlineData("soon")]
    public void ABadTime_CannotBeAdded(string text)
    {
        var (vm, _, _, _) = Make();
        vm.NewTime = text;
        Assert.False(vm.AddCommand.CanExecute(null));
    }

    [Fact]
    public void TheList_IsOrderedByTime_AndSwitchingOneOffOrDeletingIt_Works()
    {
        var (vm, _, _, _) = Make();
        vm.NewTime = "09:00"; vm.AddCommand.Execute(null);
        vm.NewTime = "06:15"; vm.AddCommand.Execute(null);
        Assert.Equal(["06:15", "09:00"], vm.Items.Select(i => i.TimeText));

        vm.Items[0].Enabled = false;
        Assert.False(vm.Items[0].Enabled);

        vm.Items[1].DeleteCommand.Execute(null);
        Assert.Single(vm.Items);
    }

    [Fact]
    public void WhenAnAlarmFires_ABannerShowsAndTheSoundPlays_UntilItIsDismissed()
    {
        var (vm, alerts, time, sound) = Make();
        vm.NewTime = "08:00";
        vm.NewLabel = "Wake up";
        vm.AddCommand.Execute(null);

        time.Advance(TimeSpan.FromHours(1));
        vm.Tick();

        Assert.NotNull(alerts.Current);
        Assert.Equal("Wake up", alerts.Current!.Title);
        Assert.Equal("08:00", alerts.Current.Message);
        Assert.Equal(1, sound.Plays);

        alerts.DismissCommand.Execute(null);
        Assert.Null(alerts.Current);
    }

    [Fact]
    public void AnAlarmWithoutALabel_IsCalledAlarm()
    {
        var (vm, alerts, time, _) = Make();
        vm.NewTime = "08:00"; vm.AddCommand.Execute(null);
        time.Advance(TimeSpan.FromHours(1));
        vm.Tick();
        Assert.Equal("Alarm", alerts.Current!.Title);
    }

    [Fact]
    public void SeveralAlerts_QueueUp_AndAreShownOneAtATime()
    {
        var alerts = new AlertsViewModel(new CountingSound());
        alerts.Show("One", "a");
        alerts.Show("Two", "b");
        Assert.Equal("One", alerts.Current!.Title);
        Assert.Equal(1, alerts.WaitingCount);

        alerts.DismissCommand.Execute(null);
        Assert.Equal("Two", alerts.Current!.Title);
        alerts.DismissCommand.Execute(null);
        Assert.Null(alerts.Current);
    }
}
