using System.Net;
using Chisel.Core.Weather;
using Chisel.Infrastructure.Weather;
using Xunit;

namespace Chisel.Infrastructure.Tests.Weather;

public class OpenMeteoTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class MemoryStore(WeatherLocation? location) : IWeatherStore
    {
        public WeatherLocation? GetLocation() => location;
        public void SaveLocation(WeatherLocation? value) => location = value;
        public CachedWeather? GetCache() => null;
        public void SaveCache(WeatherSnapshot snapshot, DateTimeOffset fetchedAt) { }
    }

    private const string Forecast = """
    { "current": { "time": "2000-01-01T09:15", "temperature_2m": 20.4, "apparent_temperature": 18.2, "weather_code": 0 },
      "hourly": { "time": ["2000-01-01T09:00"], "temperature_2m": [20.0], "weather_code": [0] },
      "daily": { "temperature_2m_max": [24.0], "temperature_2m_min": [12.0] } }
    """;

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    [Fact]
    public async Task Weather_SendsTheForecastRequest_AndParsesTheReply()
    {
        var handler = new FakeHandler(_ => Ok(Forecast));
        var service = new OpenMeteoWeatherService(new MemoryStore(new WeatherLocation("Testville", 12.35, -45.68)), handler);

        var snapshot = await service.GetAsync();

        Assert.Equal(20.4, snapshot.TemperatureC);
        var uri = Assert.Single(handler.Requests);
        Assert.Equal("https://api.open-meteo.com/v1/forecast", uri.GetLeftPart(UriPartial.Path));
        var query = uri.Query;
        Assert.Contains("latitude=12.35", query);
        Assert.Contains("longitude=-45.68", query);
        Assert.Contains("current=temperature_2m,apparent_temperature,weather_code", query);
        Assert.Contains("hourly=temperature_2m,weather_code", query);
        Assert.Contains("daily=temperature_2m_max,temperature_2m_min", query);
        Assert.Contains("timezone=auto", query);
        Assert.Contains("forecast_days=2", query);
        Assert.Contains("temperature_unit=celsius", query);
    }

    [Fact]
    public async Task Weather_WithoutALocation_Throws_AndMakesNoRequest()
    {
        var handler = new FakeHandler(_ => Ok(Forecast));
        var service = new OpenMeteoWeatherService(new MemoryStore(null), handler);

        await Assert.ThrowsAsync<WeatherLocationMissingException>(() => service.GetAsync());
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Weather_ServerError_Throws()
    {
        var service = new OpenMeteoWeatherService(new MemoryStore(new WeatherLocation("Testville", 1, 2)),
            new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetAsync());
    }

    [Fact]
    public async Task Geocoder_SendsTheSearch_AndParsesThePlaces()
    {
        var handler = new FakeHandler(_ => Ok("""{ "results": [ { "name": "Testville", "latitude": 1.234, "longitude": 5.678, "country": "Testland" } ] }"""));
        var geocoder = new OpenMeteoGeocoder(handler);

        var places = await geocoder.SearchAsync("Test ville");

        Assert.Equal("Testville, Testland", Assert.Single(places).DisplayText);
        var uri = Assert.Single(handler.Requests);
        Assert.Equal("https://geocoding-api.open-meteo.com/v1/search", uri.GetLeftPart(UriPartial.Path));
        Assert.Contains("name=Test%20ville", uri.Query);
        Assert.Contains("count=5", uri.Query);
    }
}
