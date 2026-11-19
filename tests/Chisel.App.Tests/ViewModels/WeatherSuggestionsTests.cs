using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chisel.App.ViewModels;
using Chisel.App.Services;
using Chisel.Core.Weather;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

public class WeatherSuggestionsTests
{
    private sealed class FakeGeocoder : IWeatherGeocoder
    {
        public List<string> Requests { get; } = [];
        public bool Fail { get; set; }
        public IReadOnlyList<GeocodingResult> Results { get; set; } = [];

        public Task<IReadOnlyList<GeocodingResult>> SearchAsync(string name, CancellationToken cancellationToken = default)
        {
            Requests.Add(name);
            if (Fail)
            {
                throw new InvalidOperationException("offline");
            }
            return Task.FromResult(Results);
        }
    }

    private static (WeatherSettingsViewModel Vm, FakeGeocoder Geocoder, FakeTimeProvider Time, MemoryWeatherStore Store) Create()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var store = new MemoryWeatherStore();
        var geocoder = new FakeGeocoder();
        var weather = new WeatherViewModel(new FakeWeatherService(), time, store);
        return (new WeatherSettingsViewModel(store, geocoder, weather, time), geocoder, time, store);
    }

    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task ShortQuery_SendsNoRequest()
    {
        var (vm, geocoder, _, _) = Create();

        var result = await vm.SuggestAsync("ab", CancellationToken.None);

        Assert.Empty(result);
        Assert.Empty(geocoder.Requests);
    }

    [Fact]
    public async Task NewerQuery_SupersedesOlderOne()
    {
        var (vm, geocoder, time, _) = Create();
        geocoder.Results = [new GeocodingResult("Testville", null, "Testland", 1, 2)];

        var older = vm.SuggestAsync("Test", CancellationToken.None);
        var newer = vm.SuggestAsync("Testv", CancellationToken.None);
        time.Advance(Debounce);

        Assert.Empty(await older);
        Assert.Single(await newer);
        Assert.Equal(["Testv"], geocoder.Requests);
    }

    [Fact]
    public async Task FiveResults_MapToSuggestions()
    {
        var (vm, geocoder, time, _) = Create();
        geocoder.Results = Enumerable.Range(1, 5)
            .Select(i => new GeocodingResult($"Town{i}", "Region", "Testland", i, i))
            .ToList();

        var task = vm.SuggestAsync("Town", CancellationToken.None);
        time.Advance(Debounce);
        var suggestions = (await task).Cast<GeocodingResult>().ToList();

        Assert.Equal(5, suggestions.Count);
        Assert.Equal("Town3, Region, Testland", suggestions[2].DisplayText);
    }

    [Fact]
    public async Task Failure_ReturnsEmptyList()
    {
        var (vm, geocoder, time, _) = Create();
        geocoder.Fail = true;

        var task = vm.SuggestAsync("Town", CancellationToken.None);
        time.Advance(Debounce);

        Assert.Empty(await task);
    }

    [Fact]
    public void PickingOne_SavesRoundedLocation_AndClearsBox()
    {
        var (vm, _, _, store) = Create();
        vm.Query = "Testv";

        vm.SelectedResult = new GeocodingResult("Testville", "Test Region", "Testland", 12.3456, -45.6789);

        Assert.Equal(new WeatherLocation("Testville", 12.35, -45.68), store.GetLocation());
        Assert.Equal("Testville", vm.ChosenText);
        Assert.Equal("", vm.Query);
        Assert.Null(vm.SelectedResult);
    }
}
