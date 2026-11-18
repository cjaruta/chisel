using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// The analog clock widget: just a face, following MainViewModel.AnalogClock while it is on screen. The face
// ignores the mouse so the whole widget stays a drag handle in edit mode.
public sealed class ClockWidgetView : WidgetView
{
    private readonly ClockFace _face = new() { IsHitTestVisible = false };
    private IDisposable[] _bindings = [];

    public ClockWidgetView()
    {
        Content = _face;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is not MainViewModel main)
        {
            return;
        }
        _bindings =
        [
            _face.Bind(ClockFace.HourAngleProperty, new Binding(nameof(AnalogClockViewModel.HourAngle)) { Source = main.AnalogClock }),
            _face.Bind(ClockFace.MinuteAngleProperty, new Binding(nameof(AnalogClockViewModel.MinuteAngle)) { Source = main.AnalogClock }),
            _face.Bind(ClockFace.SecondAngleProperty, new Binding(nameof(AnalogClockViewModel.SecondAngle)) { Source = main.AnalogClock }),
        ];
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        foreach (var binding in _bindings)
        {
            binding.Dispose();
        }
        _bindings = [];
        base.OnDetachedFromVisualTree(e);
    }
}
