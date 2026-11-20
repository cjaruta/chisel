using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class ClockSlotsViewModelTests
{
    private sealed class Sound : IAlertSound
    {
        public void Play() { }
    }

    private static readonly DateTimeOffset T0 = new(2000, 1, 1, 7, 0, 0, TimeSpan.Zero);

    private static (ClockSlotsViewModel Slots, ClockToolsViewModel Tools, AlarmsViewModel Alarms, AlertsViewModel Alerts, FakeTimeProvider Time) Make()
    {
        var time = new FakeTimeProvider(T0);
        var alerts = new AlertsViewModel(new Sound());
        var tools = new ClockToolsViewModel(null, time);
        var alarms = new AlarmsViewModel(new AlarmService(new MemoryAlarmRepository(), time), alerts);
        return (new ClockSlotsViewModel(tools, alarms), tools, alarms, alerts, time);
    }

    [Fact]
    public void TheDefaultSlots_AreTheClockPinnedAndOneScrollingSlotStartingOnTheTimer()
    {
        var (slots, _, _, _, _) = Make();
        slots.Resize(640, 300);

        Assert.True(slots.Horizontal);
        Assert.Equal(2, slots.Slots.Count);
        Assert.Equal(ClockTool.Clock, slots.Slots[0].Current);
        Assert.False(slots.Slots[0].CanCycle);
        Assert.Equal(ClockTool.Timer, slots.Slots[1].Current);
        Assert.True(slots.Slots[1].CanCycle);
    }

    [Fact]
    public void Cycling_MovesThroughTheSetAndWraps_AndAPinnedSlotStaysPut()
    {
        var (slots, _, _, _, _) = Make();
        slots.Resize(640, 300);
        var scrolling = slots.Slots[1];

        scrolling.Cycle(1);
        Assert.Equal(ClockTool.Stopwatch, scrolling.Current);
        scrolling.Cycle(1);
        scrolling.Cycle(1);
        Assert.Equal(ClockTool.Empty, scrolling.Current);
        scrolling.Cycle(1);
        Assert.Equal(ClockTool.Timer, scrolling.Current);
        scrolling.Cycle(-1);
        Assert.Equal(ClockTool.Empty, scrolling.Current);

        slots.Slots[0].Cycle(1);
        Assert.Equal(ClockTool.Clock, slots.Slots[0].Current);
    }

    [Fact]
    public void Select_JumpsToAToolInTheSet_AndIgnoresOthers()
    {
        var (slots, _, _, _, _) = Make();
        slots.Resize(640, 300);
        var scrolling = slots.Slots[1];

        scrolling.Select(ClockTool.Alarm);
        Assert.Equal(ClockTool.Alarm, scrolling.Current);
        scrolling.Select(ClockTool.Clock);
        Assert.Equal(ClockTool.Alarm, scrolling.Current);
    }

    [Fact]
    public void ARunningToolThatIsNotOnScreen_HasAnActiveDotWithItsTime()
    {
        var (slots, tools, _, _, time) = Make();
        slots.Resize(640, 300);
        var scrolling = slots.Slots[1];
        tools.AddTimerSecondsCommand.Execute(60);
        tools.TimerStartPauseCommand.Execute(null);
        tools.StopwatchStartPauseCommand.Execute(null);
        scrolling.Select(ClockTool.Alarm);
        time.Advance(TimeSpan.FromSeconds(5));
        tools.Tick();
        slots.Refresh();

        var timer = scrolling.Dots.Single(d => d.Tool == ClockTool.Timer);
        var stopwatch = scrolling.Dots.Single(d => d.Tool == ClockTool.Stopwatch);
        Assert.True(timer.IsActive);
        Assert.False(timer.IsCurrent);
        Assert.Equal("00:55", timer.Text);
        Assert.Equal("00:05", stopwatch.Text);
        Assert.True(scrolling.HasActiveOffscreen);
        Assert.False(scrolling.Dots.Single(d => d.Tool == ClockTool.Empty).IsActive);
        Assert.True(scrolling.Dots.Single(d => d.Tool == ClockTool.Alarm).IsCurrent);

        scrolling.Select(ClockTool.Timer);
        Assert.True(scrolling.HasActiveOffscreen); // the stopwatch still is
        tools.StopwatchStartPauseCommand.Execute(null);
        slots.Refresh();
        Assert.False(scrolling.HasActiveOffscreen);
    }

    [Fact]
    public void AFiringAlarm_SwitchesItsSlotToTheAlarm_AndRingsThereWithoutABanner()
    {
        var (slots, _, alarms, alerts, time) = Make();
        slots.Resize(640, 300);
        alarms.NewTime = "08:00";
        alarms.AddCommand.Execute(null);
        Assert.True(slots.AlarmReachable);

        time.Advance(TimeSpan.FromHours(1));
        alarms.Tick();

        Assert.Equal(ClockTool.Alarm, slots.Slots[1].Current);
        Assert.NotNull(alarms.Ringing);
        Assert.Null(alerts.Current);
        slots.Refresh();
        Assert.True(slots.Slots[1].Dots.Single(d => d.Tool == ClockTool.Alarm).IsActive);

        alarms.DismissRingingCommand.Execute(null);
        slots.Refresh();
        Assert.False(slots.Slots[1].Dots.Single(d => d.Tool == ClockTool.Alarm).IsActive);
    }

    [Fact]
    public void WithNoSlotShowingTheAlarm_OrWhileDetached_TheBannerIsUsed()
    {
        var (slots, tools, alarms, alerts, time) = Make();
        tools.SetSlots(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock)]));
        slots.Resize(640, 300);
        Assert.False(slots.AlarmReachable);
        alarms.NewTime = "08:00";
        alarms.AddCommand.Execute(null);

        time.Advance(TimeSpan.FromHours(1));
        alarms.Tick();
        Assert.NotNull(alerts.Current);
        Assert.Null(alarms.Ringing);

        tools.SetSlots(ClockSlotsConfig.Default);
        slots.Clear(); // the widget is not on screen
        Assert.False(slots.AlarmReachable);
        Assert.Empty(slots.Slots);
    }

    [Fact]
    public void WhenTheWidgetIsTooSmall_HiddenSlotsAreAbsorbed_AndComeBackWhenItGrows()
    {
        var (slots, tools, _, _, _) = Make();
        tools.SetSlots(new ClockSlotsConfig([
            ClockSlotConfig.Pinned(ClockTool.Clock),
            ClockSlotConfig.Pinned(ClockTool.Timer),
            ClockSlotConfig.Scrolling(ClockTool.Stopwatch, ClockTool.Alarm)]));

        slots.Resize(150, 150);
        var only = Assert.Single(slots.Slots);
        Assert.Equal([ClockTool.Clock, ClockTool.Timer, ClockTool.Stopwatch, ClockTool.Alarm], only.Tools);

        slots.Resize(700, 300);
        Assert.Equal(3, slots.Slots.Count);
        Assert.Equal([ClockTool.Timer], slots.Slots[1].Tools);
    }

    [Fact]
    public void TheChosenTool_SurvivesAResize_WhileItsSetIsUnchanged_AndChangingTheConfigRebuilds()
    {
        var (slots, tools, _, _, _) = Make();
        slots.Resize(640, 300);
        slots.Slots[1].Select(ClockTool.Stopwatch);

        slots.Resize(700, 320);
        Assert.Equal(ClockTool.Stopwatch, slots.Slots[1].Current);

        tools.SetSlots(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Alarm)]));
        Assert.Single(slots.Slots);
        Assert.Equal(ClockTool.Alarm, slots.Slots[0].Current);
    }

    [Fact]
    public void ARunningToolInAPinnedSlot_NeedsNoDot()
    {
        var (slots, tools, _, _, _) = Make();
        tools.SetSlots(new ClockSlotsConfig([ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Pinned(ClockTool.Timer)]));
        slots.Resize(640, 300);
        tools.AddTimerSecondsCommand.Execute(60);
        tools.TimerStartPauseCommand.Execute(null);
        slots.Refresh();
        Assert.False(slots.Slots[1].HasActiveOffscreen);
        Assert.False(slots.Slots[1].ShowDots);
    }
}
