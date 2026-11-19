using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Chisel.App.ViewModels;
using Chisel.Core.Weather;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class WeatherViewModelTests
{
    private static readonly WeatherSnapshot Sample = new(20, 18, WeatherCondition.Clear, 24, 12, []);

    private sealed class Service(Func<Task<WeatherSnapshot>> get) : IWeatherService
    {
        public Task<WeatherSnapshot> GetAsync(CancellationToken cancellationToken = default) => get();
    }

    private static (WeatherViewModel, FakeTimeProvider) Make(Func<Task<WeatherSnapshot>> get)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.Zero));
        return (new WeatherViewModel(new Service(get), time), time);
    }

    [Fact]
    public async Task LocationName_FollowsTheSavedLocation()
    {
        var store = new Chisel.App.Services.MemoryWeatherStore();
        store.SaveLocation(new WeatherLocation("Testville", 1, 2));
        var vm = new WeatherViewModel(new Service(() => Task.FromResult(Sample)),
            new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 9, 0, 0, TimeSpan.Zero)), store);

        vm.ShowCached();
        Assert.Equal("Testville", vm.LocationName);

        store.SaveLocation(new WeatherLocation("Otherton", 3, 4));
        vm.LocationChanged();
        Assert.Equal("Otherton", vm.LocationName);

        store.SaveLocation(null);
        vm.LocationChanged();
        Assert.Equal("", vm.LocationName);
        await Task.CompletedTask;
    }

    [Fact]
    public void StartsLoading()
    {
        var (vm, _) = Make(() => new TaskCompletionSource<WeatherSnapshot>().Task);
        Assert.Equal(WeatherState.Loading, vm.State);
        Assert.Null(vm.Snapshot);
    }

    [Fact]
    public async Task Load_ShowsTheSnapshot()
    {
        var (vm, _) = Make(() => Task.FromResult(Sample));
        await vm.LoadAsync();
        Assert.Equal(WeatherState.Ready, vm.State);
        Assert.Same(Sample, vm.Snapshot);
    }

    [Fact]
    public async Task Load_WithoutALocation_IsNoLocation()
    {
        var (vm, _) = Make(() => throw new WeatherLocationMissingException());
        await vm.LoadAsync();
        Assert.Equal(WeatherState.NoLocation, vm.State);
    }

    [Fact]
    public async Task Load_Failure_IsOffline_AndKeepsTheLastSnapshot()
    {
        var calls = 0;
        var (vm, _) = Make(() => ++calls == 1 ? Task.FromResult(Sample) : throw new HttpRequestException());
        await vm.LoadAsync();
        await vm.LoadAsync();
        Assert.Equal(WeatherState.Offline, vm.State);
        Assert.Same(Sample, vm.Snapshot);
    }

    [Fact]
    public async Task UpdatedText_FollowsTheClockOnRefresh()
    {
        var (vm, time) = Make(() => Task.FromResult(Sample));
        await vm.LoadAsync();
        Assert.Equal("Updated just now", vm.UpdatedText);

        time.Advance(TimeSpan.FromMinutes(5));
        vm.Refresh();

        Assert.Equal("Updated 5 min ago", vm.UpdatedText);
    }
}
