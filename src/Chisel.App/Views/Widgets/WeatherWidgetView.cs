using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chisel.App.ViewModels;
using Chisel.Core.Weather;

namespace Chisel.App.Views.Widgets;

// The weather widget. Small shows the temperature, icon and condition; Medium adds an hourly strip; Large adds
// feels-like, high/low and an updated footer. It never changes size itself: the strip and text are clipped.
// Everything ignores the mouse so the whole widget stays a drag handle in edit mode.
public sealed class WeatherWidgetView : WidgetView
{
    private WeatherViewModel? _weather;

    public WeatherWidgetView()
    {
        // Same bottom padding as the clock so content sits evenly under the heading.
        Padding = new Thickness(16, 0, 16, 12);
        ClipToBounds = true;
        IsHitTestVisible = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is MainViewModel main)
        {
            _weather = main.Weather;
            _weather.PropertyChanged += OnWeatherChanged;
        }
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_weather is not null)
        {
            _weather.PropertyChanged -= OnWeatherChanged;
            _weather = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Rebuild();
    }

    private void OnWeatherChanged(object? sender, PropertyChangedEventArgs e) => Rebuild();

    private IBrush Brush(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var found) && found is IBrush brush ? brush : fallback;

    private TextBlock Text(string text, double size, bool dim = false, string? name = null, FontWeight weight = FontWeight.Normal) =>
        new()
        {
            Text = text, FontSize = size, FontWeight = weight, Name = name,
            Foreground = dim ? Brush("DimTextBrush", Brushes.Gray) : Brush("CurrentBlockOutlineBrush", Brushes.White),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

    private void Rebuild()
    {
        if (_weather is null)
        {
            return;
        }
        var width = Bounds.Width - Padding.Left - Padding.Right;
        var height = Bounds.Height - Padding.Bottom;
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 10 };

        if (_weather.State != WeatherState.Ready || _weather.Snapshot is not { } snapshot)
        {
            ShowMessage(panel);
            return;
        }
        var tier = WeatherLayout.SizeTier(width, height);

        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        head.Children.Add(new ConditionIcon { Condition = snapshot.Condition, Width = 44, Height = 44, Name = "WeatherIcon" });
        var headText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        headText.Children.Add(Text(WeatherLayout.FormatTemperature(snapshot.TemperatureC), 40, name: "WeatherTemp", weight: FontWeight.Light));
        headText.Children.Add(Text(ConditionName(snapshot.Condition), 14, dim: true, name: "WeatherCondition"));
        head.Children.Add(headText);
        panel.Children.Add(head);

        if (tier >= WeatherSizeTier.Large)
        {
            panel.Children.Add(Text($"Feels like {WeatherLayout.FormatTemperature(snapshot.FeelsLikeC)}", 13, name: "WeatherFeelsLike"));
            panel.Children.Add(Text(
                $"High {WeatherLayout.FormatTemperature(snapshot.HighC)}  ·  Low {WeatherLayout.FormatTemperature(snapshot.LowC)}", 13, name: "WeatherHighLow"));
        }

        if (tier >= WeatherSizeTier.Medium)
        {
            var strip = new StackPanel { Orientation = Orientation.Horizontal, Name = "WeatherHourly", ClipToBounds = true };
            foreach (var hour in snapshot.Hourly.Take(WeatherLayout.HourlyCount(width)))
            {
                var cell = new StackPanel { Width = WeatherLayout.HourlyEntryWidth, Spacing = 3 };
                cell.Children.Add(Text(hour.Time.ToString("HH:mm"), 11, dim: true));
                cell.Children.Add(new ConditionIcon { Condition = hour.Condition, Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Left });
                cell.Children.Add(Text(WeatherLayout.FormatTemperature(hour.TemperatureC), 13));
                strip.Children.Add(cell);
            }
            panel.Children.Add(strip);
        }

        if (tier >= WeatherSizeTier.Large)
        {
            panel.Children.Add(Text(_weather.UpdatedText, 11, dim: true, name: "WeatherFooter"));
        }
        Content = panel;
    }

    // Loading, no location and offline each show one short message (offline adds when it last updated).
    private void ShowMessage(StackPanel panel)
    {
        var message = _weather!.State switch
        {
            WeatherState.NoLocation => "Set a location to see the weather.",
            WeatherState.Offline => "Weather is offline.",
            _ => "Loading weather…",
        };
        panel.Children.Add(Text(message, 14, dim: true, name: "WeatherMessage"));
        if (_weather.State == WeatherState.Offline && _weather.UpdatedText != "")
        {
            panel.Children.Add(Text(_weather.UpdatedText, 12, dim: true, name: "WeatherUpdated"));
        }
        Content = panel;
    }

    private static string ConditionName(WeatherCondition c) => c switch
    {
        WeatherCondition.Clear => "Clear",
        WeatherCondition.PartlyCloudy => "Partly cloudy",
        WeatherCondition.Cloudy => "Cloudy",
        WeatherCondition.Rain => "Rain",
        WeatherCondition.Snow => "Snow",
        WeatherCondition.Storm => "Storm",
        _ => "Fog",
    };
}

// A simple outline icon for one condition, drawn from a 24x24 path.
public sealed class ConditionIcon : Control
{
    public static readonly StyledProperty<WeatherCondition> ConditionProperty =
        AvaloniaProperty.Register<ConditionIcon, WeatherCondition>(nameof(Condition));

    static ConditionIcon() => AffectsRender<ConditionIcon>(ConditionProperty);

    public WeatherCondition Condition
    {
        get => GetValue(ConditionProperty);
        set => SetValue(ConditionProperty, value);
    }

    private const string CloudAt14 = "M7,14 H17 A3.5,3.5 0 0 0 17,7 A5,5 0 0 0 8,8.5 A2.75,2.75 0 0 0 7,14";

    private static string PathFor(WeatherCondition c) => c switch
    {
        WeatherCondition.Clear => "M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8 M12,2 V4 M12,20 V22 M2,12 H4 M20,12 H22 M5,5 L6.5,6.5 M17.5,17.5 L19,19 M5,19 L6.5,17.5 M17.5,6.5 L19,5",
        WeatherCondition.PartlyCloudy => "M8,3 V4.5 M2.5,8.5 H4 M4,4.5 L5,5.5 M12,4.5 L11,5.5 M8,6.5 A2.5,2.5 0 0 0 8,11.5 M8,20 H17 A3.5,3.5 0 0 0 17,13 A5,5 0 0 0 8,14.5 A2.75,2.75 0 0 0 8,20",
        WeatherCondition.Cloudy => "M7,18 H17 A4,4 0 0 0 17,10 A6,6 0 0 0 6,12 A3,3 0 0 0 7,18",
        WeatherCondition.Rain => CloudAt14 + " M8,17 L7,20 M12,17 L11,20 M16,17 L15,20",
        WeatherCondition.Snow => CloudAt14 + " M8,17.5 V17.6 M12,20 V20.1 M16,17.5 V17.6",
        WeatherCondition.Storm => CloudAt14 + " M13,14 L10,18 H14 L11,22",
        _ => "M4,8 H20 M3,12 H21 M6,16 H18 M9,20 H15",
    };

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }
        var stroke = this.TryFindResource("CurrentBlockOutlineBrush", ActualThemeVariant, out var found) && found is IBrush brush
            ? brush : Brushes.White;
        var pen = new Pen(stroke, 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(size / 24, size / 24)))
        {
            context.DrawGeometry(null, pen, Geometry.Parse(PathFor(Condition)));
        }
    }
}
