using System;
using System.Threading.Tasks;
using Chisel.Core.Settings;
using Chisel.Core.Weather;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

public enum WeatherState { Loading, Ready, NoLocation, Offline }

// The weather widget's state. It has no timer of its own: Refresh() is called from the window's shared 1-second
// refresh, updates the "updated N min ago" text, and starts a fetch when one is due. Fetches only happen while the
// widget is on the dashboard (IsActive), every 20 minutes, or 5 minutes after a failure.
public partial class WeatherViewModel : ViewModelBase
{
    public static readonly TimeSpan FetchInterval = TimeSpan.FromMinutes(20);
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);

    private readonly IWeatherService _service;
    private readonly TimeProvider _time;
    private readonly IWeatherStore? _store;
    private readonly TimeDisplay _display;
    private DateTimeOffset? _updatedAt;
    private DateTimeOffset? _nextFetchAt;
    private bool _fetching;
    private int _generation;

    [ObservableProperty]
    public partial WeatherState State { get; private set; } = WeatherState.Loading;

    // Data stays in Celsius; the unit only changes the text. Changing it raises PropertyChanged so the widget redraws.
    [ObservableProperty]
    public partial TemperatureUnit Unit { get; set; }

    // Hourly strip labels follow the shared clock format; changing it redraws the widget without fetching.
    public string HourLabel(TimeOnly time) => _display.Hour(time);

    public string Format(double celsius) => WeatherLayout.FormatTemperature(celsius, Unit);

    // The saved location's name ("" when none), shown small in the widget when its title is hidden.
    [ObservableProperty]
    public partial string LocationName { get; private set; } = "";

    private void RefreshLocationName() => LocationName = _store?.GetLocation()?.Name ?? "";

    [ObservableProperty]
    public partial WeatherSnapshot? Snapshot { get; private set; }

    [ObservableProperty]
    public partial string UpdatedText { get; private set; } = "";

    // True while the widget is on the dashboard. Turning it on makes the next Refresh fetch.
    public bool IsActive
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }
            field = value;
            if (value && _nextFetchAt != DateTimeOffset.MaxValue)
            {
                _nextFetchAt = null;
            }
        }
    }

    public WeatherViewModel(IWeatherService service, TimeProvider time, IWeatherStore? store = null, TimeDisplay? display = null)
    {
        _display = display ?? new TimeDisplay();
        _display.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HourLabel));
        _service = service;
        _time = time;
        _store = store;
    }

    // At app start: shows the saved snapshot straight away (or NoLocation); the first fetch follows once active.
    public void ShowCached()
    {
        if (_store is null)
        {
            return;
        }
        RefreshLocationName();
        if (_store.GetLocation() is null)
        {
            Show(WeatherState.NoLocation);
            _nextFetchAt = null;
            return;
        }
        if (_store.GetCache() is { } cache)
        {
            Show(WeatherState.Ready, cache.Snapshot, cache.FetchedAt);
            _nextFetchAt = null;
        }
    }

    // The saved location was set or cleared: forget the old data and fetch again.
    public void LocationChanged()
    {
        _generation++;
        RefreshLocationName();
        _fetching = false;
        _nextFetchAt = null;
        Show(WeatherState.Loading);
        _nextFetchAt = null;
        if (IsActive)
        {
            _ = LoadAsync();
        }
    }

    public async Task LoadAsync()
    {
        if (_fetching)
        {
            return;
        }
        _fetching = true;
        var generation = _generation;
        var delay = FetchInterval;
        try
        {
            var snapshot = await _service.GetAsync();
            if (generation != _generation)
            {
                return;
            }
            Snapshot = snapshot;
            _updatedAt = _time.GetUtcNow();
            _store?.SaveCache(snapshot, _updatedAt.Value);
            State = WeatherState.Ready;
        }
        catch (WeatherLocationMissingException)
        {
            if (generation != _generation)
            {
                return;
            }
            State = WeatherState.NoLocation;
        }
        catch (Exception)
        {
            if (generation != _generation)
            {
                return;
            }
            State = WeatherState.Offline;
            delay = RetryInterval;
        }
        finally
        {
            if (generation == _generation)
            {
                _fetching = false;
            }
        }
        _nextFetchAt = _time.GetUtcNow() + delay;
        Refresh();
    }

    // For the window to show a state directly (also used by tests). Nothing is fetched until the location changes.
    public void Show(WeatherState state, WeatherSnapshot? snapshot = null, DateTimeOffset? updatedAt = null)
    {
        Snapshot = snapshot;
        _updatedAt = updatedAt;
        State = state;
        _nextFetchAt = DateTimeOffset.MaxValue;
        Refresh();
    }

    public void Refresh()
    {
        if (_updatedAt is not { } at)
        {
            UpdatedText = "";
        }
        else
        {
            var minutes = (int)(_time.GetUtcNow() - at).TotalMinutes;
            UpdatedText = minutes < 1 ? "Updated just now" : minutes < 60 ? $"Updated {minutes} min ago" : $"Updated {minutes / 60} h ago";
        }

        if (IsActive && !_fetching && (_nextFetchAt is null || _time.GetUtcNow() >= _nextFetchAt))
        {
            _ = LoadAsync();
        }
    }
}
