using System;
using System.Threading;
using System.Threading.Tasks;
using Chisel.App.ViewModels;
using Chisel.Core.Settings;
using Chisel.Core.Weather;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class WeatherHourFormatTests
{
    private sealed class CountingService : IWeatherService
    {
        public int Calls;

        public Task<WeatherSnapshot> GetAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new WeatherSnapshot(20, 18, WeatherCondition.Clear, 24, 12, []));
        }
    }

    [Fact]
    public void HourLabels_FollowTheClockFormat_AndChangeLiveWithoutFetching()
    {
        var service = new CountingService();
        var display = new TimeDisplay { Format = ClockFormat.TwentyFourHour };
        var weather = new WeatherViewModel(service, new FakeTimeProvider(), null, display);
        var changed = 0;
        weather.PropertyChanged += (_, _) => changed++;

        Assert.Equal("15:00", weather.HourLabel(new TimeOnly(15, 0)));

        display.Format = ClockFormat.TwelveHour;

        Assert.Equal("3 PM", weather.HourLabel(new TimeOnly(15, 0)));
        Assert.Equal("12 AM", weather.HourLabel(new TimeOnly(0, 0)));
        Assert.True(changed > 0);
        Assert.Equal(0, service.Calls);
    }
}
