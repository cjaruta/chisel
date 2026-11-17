using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Chisel.App.Models;

namespace Chisel.App.Converters;

// A BlockColor key to the brush for a block's fill or its left stripe.
public sealed class BlockColorConverter : IValueConverter
{
    public static readonly BlockColorConverter Fill = new(stripe: false);
    public static readonly BlockColorConverter Stripe = new(stripe: true);

    private readonly bool _stripe;
    private readonly Dictionary<string, IBrush> _brushes = new();

    private BlockColorConverter(bool stripe) => _stripe = stripe;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var swatch = BlockPalette.Get(value as string);
        if (!_brushes.TryGetValue(swatch.Key, out var brush))
        {
            brush = new ImmutableSolidColorBrush(_stripe ? swatch.Stripe : swatch.Fill);
            _brushes[swatch.Key] = brush;
        }
        return brush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
