using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Chisel.App.ViewModels;

namespace Chisel.App.Converters;

// Turns a settings page into its view. The view inherits the window's DataContext (MainViewModel).
public sealed class PageViewConverter : IValueConverter
{
    public static readonly PageViewConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is SettingsPage page ? (Control?)Activator.CreateInstance(page.ViewType) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
