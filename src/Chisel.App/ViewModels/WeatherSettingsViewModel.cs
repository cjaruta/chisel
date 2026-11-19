using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chisel.Core.Weather;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// The Weather page in the Widgets settings: type a place name, pick a suggestion, or clear it.
public partial class WeatherSettingsViewModel : ViewModelBase
{
    private readonly IWeatherStore _store;
    private readonly IWeatherGeocoder _geocoder;
    private readonly WeatherViewModel _weather;
    private readonly TimeProvider _time;
    private CancellationTokenSource? _pending;

    private const int MinQueryLength = 3;
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    public string Credit => WeatherLayout.Credit;

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial GeocodingResult? SelectedResult { get; set; }

    [ObservableProperty]
    public partial string ChosenText { get; private set; } = "";

    public WeatherSettingsViewModel(IWeatherStore store, IWeatherGeocoder geocoder, WeatherViewModel weather, TimeProvider? time = null)
    {
        _store = store;
        _geocoder = geocoder;
        _weather = weather;
        _time = time ?? TimeProvider.System;
        ShowChosen();
    }

    partial void OnSelectedResultChanged(GeocodingResult? value)
    {
        if (value is null)
        {
            return;
        }
        _pending?.Cancel();
        _store.SaveLocation(value.ToLocation());
        ShowChosen();
        _weather.LocationChanged();
        SelectedResult = null;
        Query = "";
    }

    // Used by the AutoCompleteBox: waits for a pause in typing, then looks up to 5 places.
    // Any failure or a newer keystroke gives an empty list.
    public async Task<IEnumerable<object>> SuggestAsync(string? text, CancellationToken cancellationToken)
    {
        _pending?.Cancel();
        var query = text?.Trim() ?? "";
        if (query.Length < MinQueryLength)
        {
            return [];
        }
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pending = source;
        try
        {
            await Task.Delay(Debounce, _time, source.Token);
            var places = await _geocoder.SearchAsync(query, source.Token);
            return source.IsCancellationRequested ? [] : places.Cast<object>().ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    [RelayCommand]
    private void Clear()
    {
        _store.SaveLocation(null);
        ShowChosen();
        _weather.LocationChanged();
    }

    private void ShowChosen() => ChosenText = _store.GetLocation()?.Name ?? "No location set";
}
