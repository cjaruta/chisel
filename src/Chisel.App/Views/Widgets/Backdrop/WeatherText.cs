using Avalonia.Media;
using Avalonia.Media.Immutable;
using Chisel.Core.Weather;

namespace Chisel.App.Views.Widgets.Backdrop;

// The weather widget's secondary text color (light grey), so the backdrops can be brighter than the app's dim grey would allow.
public static class WeatherText
{
    public static readonly IBrush Secondary = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000 | WeatherBackdrop.SecondaryText));
}
