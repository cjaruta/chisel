using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class AlarmRingingTests
{
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
        var vm = new AlarmsViewModel(new AlarmService(new MemoryAlarmRepository(), time), alerts);
        vm.IsShownOnWidget = () => true;
        vm.NewTime = "08:00";
        vm.NewLabel = "Wake up";
        vm.AddCommand.Execute(null);
        return (vm, alerts, time, sound);
    }

    [Fact]
    public void WhenAWidgetCanShowTheAlarm_ItRingsThereInsteadOfInABanner_WithTheSound()
    {
        var (vm, alerts, time, sound) = Make();

        time.Advance(TimeSpan.FromHours(1));
        vm.Tick();

        Assert.Null(alerts.Current);
        Assert.Equal("Wake up", vm.Ringing!.Label);
        Assert.Equal("08:00", vm.RingingTime);
        Assert.Equal(1, sound.Plays);

        vm.DismissRingingCommand.Execute(null);
        Assert.Null(vm.Ringing);
    }

    [Fact]
    public void Snoozing_SilencesItNow_AndRingsAgainAfterFiveMinutes()
    {
        var (vm, _, time, sound) = Make();
        time.Advance(TimeSpan.FromHours(1));
        vm.Tick();

        vm.SnoozeCommand.Execute(null);
        Assert.Null(vm.Ringing);

        time.Advance(TimeSpan.FromMinutes(4));
        vm.Tick();
        Assert.Null(vm.Ringing);

        time.Advance(TimeSpan.FromMinutes(1));
        vm.Tick();
        Assert.NotNull(vm.Ringing);
        Assert.Equal(2, sound.Plays);
    }

    [Fact]
    public void Dismissing_CancelsAPendingSnooze()
    {
        var (vm, _, time, _) = Make();
        time.Advance(TimeSpan.FromHours(1));
        vm.Tick();
        vm.SnoozeCommand.Execute(null);
        time.Advance(TimeSpan.FromMinutes(5));
        vm.Tick();
        vm.DismissRingingCommand.Execute(null);

        time.Advance(TimeSpan.FromMinutes(30));
        vm.Tick();

        Assert.Null(vm.Ringing);
    }
}
