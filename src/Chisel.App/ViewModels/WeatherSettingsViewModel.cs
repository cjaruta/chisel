using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chisel.Core.Weather;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// The Weather page in the Widgets settings: search for a place, pick one, or clear it.
public partial class WeatherSettingsViewModel : ViewModelBase
{
    private readonly IWeatherStore _store;
    private readonly IWeatherGeocoder _geocoder;
    private readonly WeatherViewModel _weather;

    public string Credit => WeatherLayout.Credit;

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<GeocodingResult> Results { get; private set; } = [];

    [ObservableProperty]
    public partial GeocodingResult? SelectedResult { get; set; }

    [ObservableProperty]
    public partial string ChosenText { get; private set; } = "";

    [ObservableProperty]
    public partial string Message { get; private set; } = "";

    public WeatherSettingsViewModel(IWeatherStore store, IWeatherGeocoder geocoder, WeatherViewModel weather)
    {
        _store = store;
        _geocoder = geocoder;
        _weather = weather;
        ShowChosen();
    }

    partial void OnSelectedResultChanged(GeocodingResult? value)
    {
        if (value is null)
        {
            return;
        }
        _store.SaveLocation(value.ToLocation());
        Results = [];
        Message = "";
        ShowChosen();
        _weather.LocationChanged();
        SelectedResult = null;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        Message = "";
        if (string.IsNullOrWhiteSpace(Query))
        {
            return;
        }
        try
        {
            Results = await _geocoder.SearchAsync(Query);
            Message = Results.Count == 0 ? "No places found." : "";
        }
        catch (Exception)
        {
            Results = [];
            Message = "Search failed. Check your connection.";
        }
    }

    [RelayCommand]
    private void Clear()
    {
        _store.SaveLocation(null);
        Results = [];
        Message = "";
        ShowChosen();
        _weather.LocationChanged();
    }

    private void ShowChosen() => ChosenText = _store.GetLocation()?.Name ?? "No location set";
}
