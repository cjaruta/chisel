using Chisel.App.ViewModels;
using Chisel.Core.Clock;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class ClockToolsViewModelTests
{
    private sealed class MemoryTools : IClockToolsRepository
    {
        public ClockToolsState Saved { get; private set; } = new(ClockSlotsConfig.Default, TimerState.Idle, StopwatchState.Idle);
        public int Saves { get; private set; }
        public ClockToolsState Get() => Saved;
        public void Save(ClockToolsState state) { Saved = state; Saves++; }
    }

    private static readonly DateTimeOffset T0 = new(2000, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static (ClockToolsViewModel Vm, FakeTimeProvider Time, MemoryTools Repo) Make()
    {
        var time = new FakeTimeProvider(T0);
        var repo = new MemoryTools();
        return (new ClockToolsViewModel(repo, time), time, repo);
    }

    [Fact]
    public void Timer_CountsDown_PausesAndResumes()
    {
        var (vm, time, _) = Make();
        vm.AddTimerSecondsCommand.Execute(60);
        vm.AddTimerSecondsCommand.Execute(60);
        Assert.Equal("02:00", vm.TimerText);

        vm.TimerStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromSeconds(30));
        vm.Tick();
        Assert.Equal("01:30", vm.TimerText);

        vm.TimerStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromMinutes(10));
        vm.Tick();
        Assert.Equal("01:30", vm.TimerText);
        Assert.Equal(TimerStatus.Paused, vm.TimerStatus);

        vm.TimerStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromSeconds(10));
        vm.Tick();
        Assert.Equal("01:20", vm.TimerText);
    }

    [Fact]
    public void TheTimerLength_CannotBeChangedWhileItRuns_AndCanBeCleared()
    {
        var (vm, _, _) = Make();
        vm.AddTimerSecondsCommand.Execute(300);
        vm.TimerStartPauseCommand.Execute(null);
        vm.AddTimerSecondsCommand.Execute(60);
        Assert.Equal("05:00", vm.TimerText);

        vm.TimerResetCommand.Execute(null);
        vm.ClearTimerCommand.Execute(null);
        Assert.Equal("00:00", vm.TimerText);
    }

    [Fact]
    public void ReachingZero_RaisesTimerFinishedOnce_AndShowsDone()
    {
        var (vm, time, _) = Make();
        var fired = 0;
        vm.TimerFinished += () => fired++;
        vm.AddTimerSecondsCommand.Execute(10);
        vm.TimerStartPauseCommand.Execute(null);

        time.Advance(TimeSpan.FromSeconds(9));
        vm.Tick();
        Assert.Equal(0, fired);

        time.Advance(TimeSpan.FromSeconds(2));
        vm.Tick();
        vm.Tick();
        Assert.Equal(1, fired);
        Assert.Equal(TimerStatus.Finished, vm.TimerStatus);
        Assert.Equal("00:00", vm.TimerText);

        vm.TimerResetCommand.Execute(null);
        vm.TimerStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromSeconds(11));
        vm.Tick();
        Assert.Equal(2, fired);
    }

    [Fact]
    public void ARunningTimerAndStopwatch_SurviveARestartAndASleep()
    {
        var (vm, time, repo) = Make();
        vm.AddTimerSecondsCommand.Execute(600);
        vm.TimerStartPauseCommand.Execute(null);
        vm.StopwatchStartPauseCommand.Execute(null);

        time.Advance(TimeSpan.FromMinutes(4)); // the app is closed or the machine sleeps meanwhile
        var again = new ClockToolsViewModel(repo, time);

        Assert.Equal("06:00", again.TimerText);
        Assert.Equal("04:00.0", again.StopwatchText);
        Assert.Equal(TimerStatus.Running, again.TimerStatus);
    }

    [Fact]
    public void ATimerThatFinishedWhileTheAppWasClosed_ShowsDoneWithoutAlerting()
    {
        var (vm, time, repo) = Make();
        vm.AddTimerSecondsCommand.Execute(60);
        vm.TimerStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromHours(1));

        var again = new ClockToolsViewModel(repo, time);
        var fired = 0;
        again.TimerFinished += () => fired++;
        again.Tick();

        Assert.Equal(TimerStatus.Finished, again.TimerStatus);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void Stopwatch_RecordsLaps_AndResets()
    {
        var (vm, time, _) = Make();
        vm.StopwatchStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromSeconds(10));
        vm.StopwatchLapCommand.Execute(null);
        time.Advance(TimeSpan.FromSeconds(15));
        vm.StopwatchLapCommand.Execute(null);
        vm.Tick();

        Assert.Equal("00:25.0", vm.StopwatchText);
        Assert.Equal(["Lap 2   00:15.0   00:25.0", "Lap 1   00:10.0   00:10.0"], vm.LapTexts);

        vm.StopwatchResetCommand.Execute(null);
        Assert.Equal("00:00.0", vm.StopwatchText);
        Assert.Empty(vm.LapTexts);
    }

    [Fact]
    public void TheIndicator_ShowsARunningTimerOrStopwatch_AndDoneForAFinishedTimer()
    {
        var (vm, time, _) = Make();
        Assert.False(vm.IndicatorFor(ClockTool.Timer).Active);
        Assert.False(vm.IndicatorFor(ClockTool.Stopwatch).Active);

        vm.StopwatchStartPauseCommand.Execute(null);
        vm.AddTimerSecondsCommand.Execute(60);
        vm.TimerStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromSeconds(5));
        vm.Tick();
        Assert.Equal(new ToolIndicator(true, "00:55"), vm.IndicatorFor(ClockTool.Timer));
        Assert.Equal(new ToolIndicator(true, "00:05"), vm.IndicatorFor(ClockTool.Stopwatch));
        Assert.False(vm.IndicatorFor(ClockTool.Clock).Active);

        time.Advance(TimeSpan.FromMinutes(2));
        vm.Tick();
        Assert.Equal(new ToolIndicator(true, "Done"), vm.IndicatorFor(ClockTool.Timer));

        vm.StopwatchStartPauseCommand.Execute(null); // paused: nothing running
        Assert.False(vm.IndicatorFor(ClockTool.Stopwatch).Active);
    }

    [Fact]
    public void TheSlots_AreSavedKeptToThreeAndRestored()
    {
        var (vm, _, repo) = Make();
        Assert.Equal(ClockSlotsConfig.Default, vm.Slots);

        var four = new ClockSlotsConfig(Enumerable.Repeat(ClockSlotConfig.Pinned(ClockTool.Timer), 4).ToList());
        vm.SetSlots(four);

        Assert.Equal(3, vm.Slots.Slots.Count);
        Assert.Equal(vm.Slots, repo.Saved.Slots);
        Assert.Equal(vm.Slots, new ClockToolsViewModel(repo, new FakeTimeProvider(T0)).Slots);
    }

    [Fact]
    public void Ticking_DoesNotSave()
    {
        var (vm, time, repo) = Make();
        vm.StopwatchStartPauseCommand.Execute(null);
        var saves = repo.Saves;
        time.Advance(TimeSpan.FromSeconds(5));
        vm.Tick();
        Assert.Equal(saves, repo.Saves);
    }

    // 0.2 s steps, the same rule for the running time, the stopped time and every lap and split.

    [Fact]
    public void TheDisplay_StepsByTwoTenths_AndNeverRunsAheadOfTheRealTime()
    {
        var (vm, time, _) = Make();
        vm.StopwatchStartPauseCommand.Execute(null);

        var seen = new List<string>();
        for (var i = 1; i <= 25; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(100));
            vm.Tick();
            seen.Add(vm.StopwatchText);
            var real = i * 100;
            var shown = int.Parse(vm.StopwatchText[3..5]) * 1000 + int.Parse(vm.StopwatchText[^1..]) * 100;
            Assert.True(shown <= real && real - shown < 200, $"{vm.StopwatchText} at {real} ms");
        }

        Assert.All(seen, text => Assert.Contains(text[^1], "02468"));
        Assert.Equal("00:02.4", seen[^1]);
    }

    [Fact]
    public void LapsSplitsAndTheStoppedTime_UseTheSameRule()
    {
        var (vm, time, _) = Make();
        vm.StopwatchStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromMilliseconds(10_390));
        vm.StopwatchLapCommand.Execute(null); // 10.39 s: shows 10.2
        time.Advance(TimeSpan.FromMilliseconds(3_110));
        vm.StopwatchLapCommand.Execute(null); // total 13.5 s: shows 13.4; the split 3.11 s shows 3.0
        time.Advance(TimeSpan.FromMilliseconds(750));
        vm.StopwatchStartPauseCommand.Execute(null); // stops at 14.25 s: shows 14.2

        Assert.Equal(["Lap 2   00:03.0   00:13.4", "Lap 1   00:10.2   00:10.2"], vm.LapTexts);
        Assert.Equal("00:14.2", vm.StopwatchText);
    }

    // A clock whose wall time can jump (a time sync, a change of time zone) without the monotonic clock noticing.
    private sealed class SkewTime : TimeProvider
    {
        private long _ticks;
        public TimeSpan WallJump { get; set; }
        public override DateTimeOffset GetUtcNow() => T0 + TimeSpan.FromTicks(_ticks) + WallJump;
        public override long GetTimestamp() => _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }

    [Fact]
    public void TheElapsedTime_FollowsTheMonotonicClock_NotTheWallClock()
    {
        var time = new SkewTime();
        var vm = new ClockToolsViewModel(new MemoryTools(), time);
        vm.StopwatchStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromSeconds(10));

        time.WallJump = TimeSpan.FromHours(3); // the wall clock jumps forward
        vm.Tick();
        Assert.Equal("00:10.0", vm.StopwatchText);
        Assert.Equal(10, vm.StopwatchElapsedSeconds(), 3);

        time.WallJump = TimeSpan.FromHours(-2); // and then back past the start
        time.Advance(TimeSpan.FromSeconds(2.5));
        vm.StopwatchStartPauseCommand.Execute(null);
        Assert.Equal("00:12.4", vm.StopwatchText); // 12.5 s, rounded down to 12.4

        time.WallJump = TimeSpan.FromHours(1);
        vm.Tick();
        Assert.Equal("00:12.4", vm.StopwatchText); // paused: still the same
    }

    [Fact]
    public void AfterARestart_TheTimeStillCatchesUpWithTheWallClock()
    {
        var (vm, time, repo) = Make();
        vm.StopwatchStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromSeconds(30));

        var again = new ClockToolsViewModel(repo, time); // the app was closed, then opened
        time.Advance(TimeSpan.FromSeconds(5));
        again.Tick();

        Assert.Equal("00:35.0", again.StopwatchText);
    }

    [Fact]
    public void ThePausedTime_ResumesWhereItStopped()
    {
        var (vm, time, _) = Make();
        vm.StopwatchStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromSeconds(7));
        vm.StopwatchStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromMinutes(5)); // paused meanwhile

        vm.StopwatchStartPauseCommand.Execute(null);
        time.Advance(TimeSpan.FromSeconds(3));
        vm.Tick();

        Assert.Equal("00:10.0", vm.StopwatchText);
    }
}
