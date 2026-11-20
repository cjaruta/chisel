using System;
using System.Globalization;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;

namespace Chisel.App.Converters;

// A settings page that fills the height it is given is not scrolled as a whole; any other page is.
public sealed class FillsHeightConverter : IValueConverter
{
    public static readonly FillsHeightConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
