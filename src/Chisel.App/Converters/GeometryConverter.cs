using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Chisel.App.Converters;

// Path data text (like a widget's icon) to a geometry a Path can draw.
public sealed class GeometryConverter : IValueConverter
{
    public static readonly GeometryConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string data && !string.IsNullOrWhiteSpace(data) ? Geometry.Parse(data) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
