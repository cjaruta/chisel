using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class AlarmCompactTests
{
    private static (AlarmsViewModel Vm, FakeTimeProvider Time) Make()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 7, 0, 0, TimeSpan.Zero));
        var vm = new AlarmsViewModel(new AlarmService(new MemoryAlarmRepository(), time), new AlertsViewModel());
        return (vm, time);
    }

    private static void Add(AlarmsViewModel vm, string time, string label, params AlarmDays[] days)
    {
        vm.NewTime = time;
        vm.NewLabel = label;
        foreach (var day in vm.Days)
        {
            day.IsOn = days.Contains(day.Day);
        }
        vm.AddCommand.Execute(null);
    }

    [Fact]
    public void WithNoAlarms_ItInvitesTheUserToSetOne()
    {
        var (vm, _) = Make();
        Assert.False(vm.HasAlarms);
        Assert.Equal("--:--", vm.NextTimeText);
        Assert.Equal("No alarms set", vm.NextCaption);
    }

    [Fact]
    public void TheNextAlarm_IsShownLarge_WithItsLabelAndDays_AndMovesOnWhenItHasRung()
    {
        var (vm, time) = Make();
        Add(vm, "09:30", "Call");
        Add(vm, "08:00", "Wake up", AlarmDays.Monday, AlarmDays.Saturday);

        Assert.True(vm.HasAlarms);
        Assert.Equal("08:00", vm.NextTimeText);
        Assert.Equal("Wake up · Mon, Sat", vm.NextCaption);
        Assert.True(vm.NextEnabled);

        time.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(5));
        vm.Tick();
        Assert.Equal("09:30", vm.NextTimeText);
        Assert.Equal("Call · Once", vm.NextCaption);
    }

    [Fact]
    public void TheToggle_SwitchesTheShownAlarmOffAndOn_WithoutTheDisplayJumpingAway()
    {
        var (vm, _) = Make();
        Add(vm, "08:00", "Wake up", AlarmDays.Every);
        Add(vm, "09:30", "Call", AlarmDays.Every);

        vm.NextEnabled = false;
        Assert.Equal("08:00", vm.NextTimeText); // still shown, now off, so it can be switched back on
        Assert.False(vm.NextEnabled);
        Assert.False(vm.Items.Single(i => i.TimeText == "08:00").Enabled);

        vm.NextEnabled = true;
        Assert.True(vm.NextEnabled);
        Assert.True(vm.Items.Single(i => i.TimeText == "08:00").Enabled);
    }

    [Fact]
    public void WhenEveryAlarmIsOff_TheEarliestIsShownSwitchedOff()
    {
        var (vm, _) = Make();
        Add(vm, "09:30", "Call", AlarmDays.Every);
        Add(vm, "08:00", "Wake up", AlarmDays.Every);
        foreach (var item in vm.Items.ToList())
        {
            item.Enabled = false;
        }
        vm.Tick();

        Assert.Equal("08:00", vm.NextTimeText);
        Assert.False(vm.NextEnabled);
    }

    [Fact]
    public void DeletingTheShownAlarm_ShowsTheNextOne()
    {
        var (vm, _) = Make();
        Add(vm, "08:00", "A", AlarmDays.Every);
        Add(vm, "09:30", "B", AlarmDays.Every);
        vm.Items.Single(i => i.TimeText == "08:00").DeleteCommand.Execute(null);
        Assert.Equal("09:30", vm.NextTimeText);
    }

    [Fact]
    public void ManagingOpensTheFullList_AndClosesIt()
    {
        var (vm, _) = Make();
        Assert.False(vm.IsManaging);
        vm.ManageCommand.Execute(null);
        Assert.True(vm.IsManaging);
        vm.ManageCommand.Execute(null);
        Assert.False(vm.IsManaging);
    }
}
