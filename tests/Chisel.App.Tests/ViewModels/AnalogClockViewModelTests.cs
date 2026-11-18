using Chisel.App.ViewModels;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class AnalogClockViewModelTests
{
    private sealed class FakeTimer : ITickTimer
    {
        public event Action? Tick;
        public bool IsRunning { get; private set; }
        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Fire() => Tick?.Invoke();
        public bool HasSubscribers => Tick is not null;
    }

    private static FakeTimeProvider At(int h, int m, int s) =>
        new(new DateTimeOffset(2000, 1, 1, h, m, s, TimeSpan.Zero));

    [Fact]
    public void Attach_ShowsTheCurrentTimeAndStartsTheTimer()
    {
        var timer = new FakeTimer();
        var clock = new AnalogClockViewModel(At(3, 0, 0), timer);

        clock.Attach();

        Assert.True(timer.IsRunning);
        Assert.Equal((90d, 0d, 0d), (clock.HourAngle, clock.MinuteAngle, clock.SecondAngle));
    }

    [Fact]
    public void Tick_UpdatesTheAngles()
    {
        var time = At(12, 0, 0);
        var timer = new FakeTimer();
        var clock = new AnalogClockViewModel(time, timer);
        clock.Attach();

        time.Advance(TimeSpan.FromSeconds(30));
        timer.Fire();

        Assert.Equal(180, clock.SecondAngle);
        Assert.Equal(3, clock.MinuteAngle, 6);
    }

    [Fact]
    public void Detach_StopsTheTimerAndFurtherTicksDoNothing()
    {
        var time = At(12, 0, 0);
        var timer = new FakeTimer();
        var clock = new AnalogClockViewModel(time, timer);
        clock.Attach();

        clock.Detach();
        time.Advance(TimeSpan.FromSeconds(10));
        timer.Fire();

        Assert.False(timer.IsRunning);
        Assert.False(timer.HasSubscribers);
        Assert.Equal(0, clock.SecondAngle);
    }
}
