using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chisel.Core.Weather;

namespace Chisel.App.Services;

// Fixed, obviously fake values until a real service is added.
public sealed class FakeWeatherService : IWeatherService
{
    public Task<WeatherSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        WeatherCondition[] conditions =
            [WeatherCondition.PartlyCloudy, WeatherCondition.Cloudy, WeatherCondition.Rain, WeatherCondition.Storm,
             WeatherCondition.Fog, WeatherCondition.Snow, WeatherCondition.Clear, WeatherCondition.PartlyCloudy];
        var hourly = conditions.Select((c, i) => new HourlyWeather(new TimeOnly(10 + i, 0), c, 20 - i)).ToList();
        return Task.FromResult(new WeatherSnapshot(20, 18, WeatherCondition.PartlyCloudy, 24, 12, hourly));
    }
}
