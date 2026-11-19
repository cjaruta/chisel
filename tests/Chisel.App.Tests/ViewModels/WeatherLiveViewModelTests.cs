using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Chisel.App.ViewModels;
using Chisel.Core.Weather;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class WeatherLiveViewModelTests
{
    private static readonly WeatherSnapshot Fresh = new(20, 18, WeatherCondition.Clear, 24, 12, []);
    private static readonly WeatherSnapshot Old = new(10, 8, WeatherCondition.Cloudy, 14, 2, []);
    private static readonly WeatherLocation Place = new("Testville", 12.35, -45.68);

    private sealed class Service : IWeatherService
    {
        public int Calls;
        public bool Fail;
        public bool NoLocation;

        public Task<WeatherSnapshot> GetAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            if (NoLocation)
            {
                throw new WeatherLocationMissingException();
            }
            return Fail ? throw new HttpRequestException() : Task.FromResult(Fresh);
        }
    }

    private sealed class Store : IWeatherStore
    {
        public WeatherLocation? Location;
        public CachedWeather? Cache;
        public WeatherLocation? GetLocation() => Location;
        public void SaveLocation(WeatherLocation? location) { Location = location; Cache = null; }
        public CachedWeather? GetCache() => Cache;
        public void SaveCache(WeatherSnapshot snapshot, DateTimeOffset fetchedAt) => Cache = new CachedWeather(snapshot, fetchedAt);
    }

    private static (WeatherViewModel Vm, Service Service, Store Store, FakeTimeProvider Time) Make(WeatherLocation? location = null)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var service = new Service();
        var store = new Store { Location = location };
        return (new WeatherViewModel(service, time, store), service, store, time);
    }

    [Fact]
    public void Start_WithoutALocation_IsNoLocation()
    {
        var (vm, _, _, _) = Make();
        vm.ShowCached();
        Assert.Equal(WeatherState.NoLocation, vm.State);
    }

    [Fact]
    public void Start_WithACache_ShowsItAtOnce_WithItsAge()
    {
        var (vm, service, store, time) = Make(Place);
        store.Cache = new CachedWeather(Old, time.GetUtcNow().AddMinutes(-7));

        vm.ShowCached();

        Assert.Equal(WeatherState.Ready, vm.State);
        Assert.Same(Old, vm.Snapshot);
        Assert.Equal("Updated 7 min ago", vm.UpdatedText);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public void NothingIsFetched_WhileTheWidgetIsNotOnTheDashboard()
    {
        var (vm, service, _, time) = Make(Place);
        vm.ShowCached();
        time.Advance(TimeSpan.FromHours(1));
        vm.Refresh();
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public void WhenActive_FetchesAtOnce_ThenEvery20Minutes_AndSavesTheCache()
    {
        var (vm, service, store, time) = Make(Place);
        vm.ShowCached();
        vm.IsActive = true;

        vm.Refresh();
        Assert.Equal(1, service.Calls);
        Assert.Equal(WeatherState.Ready, vm.State);
        Assert.Same(Fresh, store.Cache!.Snapshot);

        time.Advance(TimeSpan.FromMinutes(19));
        vm.Refresh();
        Assert.Equal(1, service.Calls);

        time.Advance(TimeSpan.FromMinutes(1));
        vm.Refresh();
        Assert.Equal(2, service.Calls);
    }

    [Fact]
    public void Failure_KeepsTheCache_ShowsOffline_RetriesAfter5Minutes_AndRecovers()
    {
        var (vm, service, store, time) = Make(Place);
        store.Cache = new CachedWeather(Old, time.GetUtcNow().AddMinutes(-30));
        vm.ShowCached();
        vm.IsActive = true;
        service.Fail = true;

        vm.Refresh();
        Assert.Equal(WeatherState.Offline, vm.State);
        Assert.Same(Old, vm.Snapshot);
        Assert.Equal("Updated 30 min ago", vm.UpdatedText);

        time.Advance(TimeSpan.FromMinutes(4));
        vm.Refresh();
        Assert.Equal(1, service.Calls);

        service.Fail = false;
        time.Advance(TimeSpan.FromMinutes(1));
        vm.Refresh();
        Assert.Equal(2, service.Calls);
        Assert.Equal(WeatherState.Ready, vm.State);
        Assert.Same(Fresh, vm.Snapshot);
    }

    [Fact]
    public void RemovingTheWidget_StopsFetching_AndAddingItFetchesAgain()
    {
        var (vm, service, _, time) = Make(Place);
        vm.IsActive = true;
        vm.Refresh();
        vm.IsActive = false;
        time.Advance(TimeSpan.FromHours(1));
        vm.Refresh();
        Assert.Equal(1, service.Calls);

        vm.IsActive = true;
        vm.Refresh();
        Assert.Equal(2, service.Calls);
    }

    [Fact]
    public void ChangingTheLocation_DropsTheOldSnapshot_AndFetchesAtOnce()
    {
        var (vm, service, store, _) = Make(Place);
        vm.IsActive = true;
        vm.Refresh();

        store.SaveLocation(new WeatherLocation("Sampleton", 1, 2));
        service.Fail = true;
        vm.LocationChanged();

        Assert.Equal(2, service.Calls);
        Assert.Null(vm.Snapshot);
    }

    [Fact]
    public void ClearingTheLocation_ShowsNoLocation()
    {
        var (vm, service, store, _) = Make(Place);
        vm.IsActive = true;
        vm.Refresh();

        store.SaveLocation(null);
        service.NoLocation = true;
        vm.LocationChanged();

        Assert.Equal(WeatherState.NoLocation, vm.State);
        Assert.Null(vm.Snapshot);
    }
}
