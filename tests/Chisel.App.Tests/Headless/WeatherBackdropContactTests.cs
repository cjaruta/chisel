using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Chisel.Core.Weather;

namespace Chisel.App.Tests.Headless;

public class WeatherBackdropContactTests
{
    // One PNG with every scene key's gradient and its text colors, at the widget's size.
    [AvaloniaFact]
    public void Render_AllSceneKeys()
    {
        var wrap = new WrapPanel();
        foreach (var (key, gradient) in WeatherBackdrop.Gradients)
        {
            var stack = new StackPanel { Margin = new Thickness(16) };
            stack.Children.Add(new TextBlock { Text = "20°", FontSize = 40, Foreground = new SolidColorBrush(Color.Parse("#F2F4F8")) });
            stack.Children.Add(new TextBlock { Text = key, FontSize = 14, Foreground = new SolidColorBrush(Color.Parse("#8A8F98")) });
            wrap.Children.Add(new Border
            {
                Width = 280, Height = 120, Margin = new Thickness(6), CornerRadius = new CornerRadius(12), Child = stack,
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.FromUInt32(0xFF000000 | gradient.Top), 0), new GradientStop(Color.FromUInt32(0xFF000000 | gradient.Bottom), 1) },
                },
            });
        }
        var window = new Window { Width = 1200, Height = 760, Background = new SolidColorBrush(Color.Parse("#14161A")), Content = wrap };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame()!;
        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "ui");
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, "weather-backdrop-contact.png"));
    }
}
