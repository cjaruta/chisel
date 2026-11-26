using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Focus;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class FocusTimerViewModelTests
{
    private static readonly DateTimeOffset Morning = new(2000, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _clock = new(Morning);
    private readonly MemoryRepo _repo = new();
    private readonly FakeChime _chime = new();
    private readonly List<FocusTaskOption> _tasks = new() { new(Guid.NewGuid(), "Write report", false), new(Guid.NewGuid(), "Plan week", false) };

    private FocusTimerViewModel Create(bool watched = true)
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        var vm = new FocusTimerViewModel(_repo, _clock, _chime, tasks: () => _tasks);
        if (watched)
        {
            vm.Attach(this);
        }
        return vm;
    }

    [Fact]
    public void Starting_CountsDownFromTimestamps_AndNamesTheTimeForScreenReaders()
    {
        var vm = Create();
        Assert.Equal(("Focus", "25:00", "Start"), (vm.PhaseText, vm.TimeText, vm.PrimaryText));

        vm.Primary();
        _clock.Advance(TimeSpan.FromSeconds(6 * 60 + 40));

        Assert.Equal(("18:20", "Pause"), (vm.TimeText, vm.PrimaryText));
        Assert.Equal("Focus, 18 minutes 20 seconds remaining", vm.AutomationName);
        Assert.Equal(1100.0 / 1500, vm.Fraction, 6);

        vm.Primary();
        Assert.Equal(("Resume", "Focus, 18 minutes 20 seconds remaining, paused"), (vm.PrimaryText, vm.AutomationName));
    }

    [Fact]
    public void TheDisplayUpdates_OnlyWhenTheShownSecondChanges_AndNotAtAllWhileNobodyWatches()
    {
        var vm = Create();
        vm.Start();
        _clock.Advance(TimeSpan.FromMilliseconds(400));
        var before = vm.Updates;

        for (var i = 0; i < 10; i++)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(100));
        }
        Assert.Equal(before + 1, vm.Updates);
        Assert.Equal("24:59", vm.TimeText);

        vm.Detach(this);
        before = vm.Updates;
        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(before, vm.Updates);

        vm.Attach(this);
        Assert.Equal("14:59", vm.TimeText);
    }

    [Fact]
    public void APhaseEnd_ChimesAtTheChosenVolume_AndFlashesForAWhile_EvenWhenNotWatched()
    {
        var vm = Create(watched: false);
        vm.ChimeVolume = 30;
        vm.Start();
        _clock.Advance(TimeSpan.FromMinutes(25));

        Assert.Equal(new[] { 30 }, _chime.Played);
        Assert.True(vm.IsFlashing);
        Assert.Equal((FocusPhase.ShortBreak, FocusRun.Idle, "Short Break", "5:00"), (vm.Phase, vm.Run, vm.PhaseText, vm.TimeText));
        Assert.Equal("1 session today", vm.SessionsText);

        _clock.Advance(FocusTimerViewModel.FlashLength);
        Assert.False(vm.IsFlashing);
    }

    [Fact]
    public void AMutedChime_StaysSilent_AndALatePhaseEndDoesNotChime()
    {
        var vm = Create();
        vm.IsMuted = true;
        vm.Start();
        _clock.Advance(TimeSpan.FromMinutes(25));
        Assert.Empty(_chime.Played);

        vm.IsMuted = false;
        vm.Start();
        vm.Dispose();
        _clock.Advance(TimeSpan.FromHours(2)); // the break ended long ago while the app was closed
        var restarted = Create();
        Assert.Empty(_chime.Played);
        Assert.Equal((FocusPhase.Focus, true), (restarted.Phase, restarted.IsFlashing));
    }

    [Fact]
    public void SkipAndReset_Work_AndClearTheFlash()
    {
        var vm = Create();
        vm.Start();
        _clock.Advance(TimeSpan.FromMinutes(25));
        Assert.True(vm.IsFlashing);

        vm.Skip();
        Assert.Equal((FocusPhase.Focus, false), (vm.Phase, vm.IsFlashing));
        vm.Start();
        _clock.Advance(TimeSpan.FromMinutes(3));
        vm.Reset();
        Assert.Equal((FocusPhase.Focus, FocusRun.Idle, "25:00", 0, 1), (vm.Phase, vm.Run, vm.TimeText, vm.DotsDone, vm.SessionsToday));
    }

    [Fact]
    public void ARunningTimer_ResumesAfterARestart()
    {
        var vm = Create();
        vm.Start();
        _clock.Advance(TimeSpan.FromMinutes(5));
        vm.Dispose();

        _clock.Advance(TimeSpan.FromMinutes(2));
        var restarted = Create();

        Assert.Equal((FocusRun.Running, "18:00"), (restarted.Run, restarted.TimeText));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("17:59", restarted.TimeText);
    }

    [Fact]
    public void Settings_AreValidated_SavedAndPresetsApply()
    {
        var vm = Create();

        vm.FocusMinutes = 0;
        Assert.Equal("Focus must be 1 to 180 minutes.", vm.SettingsError);
        Assert.Equal(25, vm.FocusMinutes);

        vm.FocusMinutes = 30;
        Assert.Null(vm.SettingsError);
        Assert.Equal(("30:00", 30), (vm.TimeText, _repo.Saved.Settings.FocusMinutes));

        vm.ApplyPreset(FocusPresets.All[2]);
        Assert.Equal((15, 3, 10), (vm.FocusMinutes, vm.ShortBreakMinutes, vm.LongBreakMinutes));
        vm.SessionsBeforeLongBreak = 3;
        vm.AutoStartNext = true;
        vm.IsDigital = true;
        Assert.Equal(FocusSettings.Default with { FocusMinutes = 15, ShortBreakMinutes = 3, LongBreakMinutes = 10, SessionsBeforeLongBreak = 3, AutoStartNext = true, Face = FocusFace.Digital },
            _repo.Saved.Settings);
        Assert.Equal(3, vm.DotsTotal);
    }

    [Fact]
    public void TheFocusTask_IsShownByName_AndADeletedTaskIsLetGo()
    {
        var vm = Create();
        Assert.Equal(new[] { "Write report", "Plan week" }, vm.TaskOptions().Select(t => t.Name));

        vm.ChooseTask(_tasks[1].Id);
        Assert.Equal("Plan week", vm.TaskName);
        Assert.Equal(_tasks[1].Id, _repo.Saved.Settings.TaskId);

        _tasks.RemoveAt(1);
        vm.RefreshTask();
        Assert.Null(vm.TaskName);
        Assert.Null(_repo.Saved.Settings.TaskId);
    }

    [Fact]
    public void TheSessionCount_StartsOverAtMidnight_WithoutAnyAction()
    {
        _clock.SetUtcNow(new DateTimeOffset(2000, 1, 1, 23, 20, 0, TimeSpan.Zero));
        var vm = Create();
        vm.Start();
        _clock.Advance(TimeSpan.FromMinutes(25));
        Assert.Equal("1 session today", vm.SessionsText);

        _clock.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal("0 sessions today", vm.SessionsText);
    }

    [Fact]
    public void ATickThatChangesNothing_AllocatesNothing()
    {
        var vm = Create();
        vm.Start();
        vm.Tick();

        var perTick = long.MaxValue;
        for (var round = 0; round < 3; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                vm.Tick();
            }
            perTick = Math.Min(perTick, (GC.GetAllocatedBytesForCurrentThread() - before) / 1000);
        }
        Assert.True(perTick == 0, $"{perTick} B per tick");
    }

    [Fact]
    public void Dispose_StopsTheTimer()
    {
        var vm = Create();
        vm.Start();
        vm.Dispose();

        _clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Empty(_chime.Played);
    }

    private sealed class MemoryRepo : IFocusTimerRepository
    {
        public FocusTimerData Saved { get; private set; } = FocusTimerData.Default;
        public FocusTimerData Get() => Saved;
        public void Save(FocusTimerData data) => Saved = data;
    }

    private sealed class FakeChime : IChime
    {
        public List<int> Played { get; } = new();
        public void Play(int volume) => Played.Add(volume);
    }
}
