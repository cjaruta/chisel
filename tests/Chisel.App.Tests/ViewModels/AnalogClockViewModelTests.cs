using Chisel.App.ViewModels;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class AnalogClockViewModelTests
{
    private static FakeTimeProvider At(int h, int m, int s) =>
        new(new DateTimeOffset(2000, 1, 1, h, m, s, TimeSpan.Zero));

    [Fact]
    public void ShowsTheCurrentTimeFromTheStart()
    {
        var clock = new AnalogClockViewModel(new ClockViewModel(At(3, 0, 0)));

        Assert.Equal((90d, 0d, 0d), (clock.HourAngle, clock.MinuteAngle, clock.SecondAngle));
    }

    [Fact]
    public void WhenTheHeaderClockRefreshes_TheAnglesFollow()
    {
        var time = At(12, 0, 0);
        var header = new ClockViewModel(time);
        var clock = new AnalogClockViewModel(header);

        time.Advance(TimeSpan.FromSeconds(30));
        header.Refresh();

        Assert.Equal(180, clock.SecondAngle);
        Assert.Equal(3, clock.MinuteAngle, 6);
    }

    [Fact]
    public void NeedsRefresh_IsFalseWithinTheSameSecond()
    {
        var time = At(12, 0, 0);
        var header = new ClockViewModel(time);

        time.Advance(TimeSpan.FromMilliseconds(900));

        Assert.False(header.NeedsRefresh);
    }

    [Fact]
    public void NeedsRefresh_IsTrueOnceTheSecondChanges_AndRefreshingClearsIt()
    {
        var time = At(12, 0, 0);
        var header = new ClockViewModel(time);
        var changes = 0;
        header.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(ClockViewModel.Now) ? 1 : 0;

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(header.NeedsRefresh);
        header.Refresh();

        Assert.False(header.NeedsRefresh);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void JumpingTwoSeconds_NeedsOneRefresh_AndTheAngleIsRight()
    {
        var time = At(12, 0, 0);
        var header = new ClockViewModel(time);
        var clock = new AnalogClockViewModel(header);

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.True(header.NeedsRefresh);
        header.Refresh();

        Assert.False(header.NeedsRefresh);
        Assert.Equal(12, clock.SecondAngle);
    }
}