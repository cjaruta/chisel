using System;
using System.Threading;
using System.Threading.Tasks;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Core.Settings;
using Chisel.Core.Weather;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class TemperatureUnitTests
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

    private sealed class FakeSettingsRepository(UserSettings? initial) : IUserSettingsRepository
    {
        public UserSettings? Saved { get; private set; } = initial;
        public UserSettings? Get() => Saved;
        public void Save(UserSettings settings) => Saved = settings;
    }

    private sealed class NoGeocoder : IWeatherGeocoder
    {
        public Task<System.Collections.Generic.IReadOnlyList<GeocodingResult>> SearchAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyList<GeocodingResult>>([]);
    }

    private static readonly UserSettings Defaults = new("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));

    private static (WeatherSettingsViewModel Settings, WeatherViewModel Weather, CountingService Service, FakeSettingsRepository Repository) Create(
        UserSettings? stored = null)
    {
        var service = new CountingService();
        var weather = new WeatherViewModel(service, new FakeTimeProvider(), new MemoryWeatherStore());
        var repository = new FakeSettingsRepository(stored ?? Defaults);
        return (new WeatherSettingsViewModel(new MemoryWeatherStore(), new NoGeocoder(), weather, null, repository), weather, service, repository);
    }

    [Fact]
    public void Default_IsCelsius()
    {
        var (settings, weather, _, _) = Create();

        Assert.True(settings.UseCelsius);
        Assert.Equal("20°", weather.Format(20));
    }

    [Fact]
    public void SavedFahrenheit_IsLoadedIntoTheWeatherViewModel()
    {
        var (settings, weather, _, _) = Create(Defaults.WithTemperatureUnit(TemperatureUnit.Fahrenheit));

        Assert.True(settings.UseFahrenheit);
        Assert.Equal("68°", weather.Format(20));
    }

    [Fact]
    public void ChangingTheUnit_ChangesTheText_WithoutFetchingAgain_AndSaves()
    {
        var (settings, weather, service, repository) = Create();
        weather.Show(WeatherState.Ready, new WeatherSnapshot(20, 18, WeatherCondition.Clear, 24, 12, []));
        var changed = 0;
        weather.PropertyChanged += (_, _) => changed++;

        settings.UseFahrenheit = true;

        Assert.Equal("68°", weather.Format(weather.Snapshot!.TemperatureC));
        Assert.Equal("64°", weather.Format(weather.Snapshot.FeelsLikeC));
        Assert.True(changed > 0);
        Assert.Equal(0, service.Calls);
        Assert.Equal(TemperatureUnit.Fahrenheit, repository.Saved!.TemperatureUnit);
        Assert.False(settings.UseCelsius);
    }
}
