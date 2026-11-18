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
        // The heading sits 12px above this view's top (header margin and its spare height). Pad the bottom by the
        // same amount so the face has equal space above and below.
        Padding = new Thickness(0, 0, 0, 12);
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
