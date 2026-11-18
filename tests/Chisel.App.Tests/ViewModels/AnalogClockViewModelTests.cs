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
}
