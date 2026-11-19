using Chisel.Core.Weather;
using Xunit;

namespace Chisel.Core.Tests.Weather;

public class WeatherLiveTests
{
    private const string Forecast = """
    {
      "current": { "time": "2000-01-01T09:15", "temperature_2m": 20.4, "apparent_temperature": 18.2, "weather_code": 2 },
      "hourly": {
        "time": ["2000-01-01T08:00","2000-01-01T09:00","2000-01-01T10:00","2000-01-01T11:00"],
        "temperature_2m": [19.0, 20.0, 21.0, 22.5],
        "weather_code": [0, 2, 61, 95]
      },
      "daily": { "temperature_2m_max": [24.0, 25.0], "temperature_2m_min": [12.0, 11.0] }
    }
    """;

    [Fact]
    public void Forecast_ParsesIntoASnapshot_WithHourlyStartingAtTheCurrentHour()
    {
        var snapshot = WeatherForecastParser.ParseForecast(Forecast);

        Assert.Equal(20.4, snapshot.TemperatureC);
        Assert.Equal(18.2, snapshot.FeelsLikeC);
        Assert.Equal(WeatherCondition.PartlyCloudy, snapshot.Condition);
        Assert.Equal(24.0, snapshot.HighC);
        Assert.Equal(12.0, snapshot.LowC);
        Assert.Equal(
            [new HourlyWeather(new TimeOnly(9, 0), WeatherCondition.PartlyCloudy, 20.0),
             new HourlyWeather(new TimeOnly(10, 0), WeatherCondition.Rain, 21.0),
             new HourlyWeather(new TimeOnly(11, 0), WeatherCondition.Storm, 22.5)],
            snapshot.Hourly);
    }

    [Theory]
    [InlineData(0, WeatherCondition.Clear)]
    [InlineData(1, WeatherCondition.PartlyCloudy)]
    [InlineData(2, WeatherCondition.PartlyCloudy)]
    [InlineData(3, WeatherCondition.Cloudy)]
    [InlineData(45, WeatherCondition.Fog)]
    [InlineData(48, WeatherCondition.Fog)]
    [InlineData(51, WeatherCondition.Rain)]
    [InlineData(67, WeatherCondition.Rain)]
    [InlineData(80, WeatherCondition.Rain)]
    [InlineData(82, WeatherCondition.Rain)]
    [InlineData(71, WeatherCondition.Snow)]
    [InlineData(77, WeatherCondition.Snow)]
    [InlineData(85, WeatherCondition.Snow)]
    [InlineData(86, WeatherCondition.Snow)]
    [InlineData(95, WeatherCondition.Storm)]
    [InlineData(99, WeatherCondition.Storm)]
    [InlineData(1234, WeatherCondition.Cloudy)]
    public void WeatherCodes_MapToConditions(int code, WeatherCondition expected) =>
        Assert.Equal(expected, WeatherCodes.ToCondition(code));

    [Fact]
    public void Geocoding_ParsesPlaces_AndAnEmptyResultIsNoPlaces()
    {
        var places = WeatherForecastParser.ParseGeocoding("""
        { "results": [
          { "name": "Testville", "latitude": 12.3456, "longitude": -45.6789, "admin1": "Test Region", "country": "Testland" },
          { "name": "Sampleton", "latitude": 1.0, "longitude": 2.0, "country": "Testland" } ] }
        """);

        Assert.Equal(2, places.Count);
        Assert.Equal("Testville, Test Region, Testland", places[0].DisplayText);
        Assert.Equal("Sampleton, Testland", places[1].DisplayText);
        Assert.Empty(WeatherForecastParser.ParseGeocoding("""{ "generationtime_ms": 0.5 }"""));
    }

    [Fact]
    public void Location_RoundsCoordinatesToTwoDecimals()
    {
        var location = WeatherLocation.Create("Testville", 12.3456, -45.6789);
        Assert.Equal(12.35, location.Latitude);
        Assert.Equal(-45.68, location.Longitude);
    }
}
