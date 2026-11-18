using System;
using System.Threading.Tasks;
using Chisel.Core.Weather;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

public enum WeatherState { Loading, Ready, NoLocation, Offline }

// The weather widget's state. Refresh() is called from the window's shared 1-second refresh and only updates
// the "updated N min ago" text; it never fetches.
public partial class WeatherViewModel : ViewModelBase
{
    private readonly IWeatherService _service;
    private readonly TimeProvider _time;
    private DateTimeOffset? _updatedAt;

    [ObservableProperty]
    public partial WeatherState State { get; private set; } = WeatherState.Loading;

    [ObservableProperty]
    public partial WeatherSnapshot? Snapshot { get; private set; }

    [ObservableProperty]
    public partial string UpdatedText { get; private set; } = "";

    public WeatherViewModel(IWeatherService service, TimeProvider time)
    {
        _service = service;
        _time = time;
    }

    public async Task LoadAsync()
    {
        try
        {
            Snapshot = await _service.GetAsync();
            _updatedAt = _time.GetUtcNow();
            State = WeatherState.Ready;
        }
        catch (WeatherLocationMissingException)
        {
            State = WeatherState.NoLocation;
        }
        catch (Exception)
        {
            State = WeatherState.Offline;
        }
        Refresh();
    }

    // For the window to show a state directly (also used by tests).
    public void Show(WeatherState state, WeatherSnapshot? snapshot = null, DateTimeOffset? updatedAt = null)
    {
        Snapshot = snapshot;
        _updatedAt = updatedAt;
        State = state;
        Refresh();
    }

    public void Refresh()
    {
        if (_updatedAt is not { } at)
        {
            UpdatedText = "";
            return;
        }
        var minutes = (int)(_time.GetUtcNow() - at).TotalMinutes;
        UpdatedText = minutes < 1 ? "Updated just now" : minutes < 60 ? $"Updated {minutes} min ago" : $"Updated {minutes / 60} h ago";
    }
}
