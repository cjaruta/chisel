using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// The analog clock widget: just a face. It ticks only while it is on screen (attached to the window), and the
// face ignores the mouse so the whole widget stays a drag handle in edit mode.
public sealed class ClockWidgetView : WidgetView
{
    private readonly ClockFace _face = new() { IsHitTestVisible = false };
    private AnalogClockViewModel? _clock;

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
        _clock = new AnalogClockViewModel(main.Clock.Time, new DispatcherTickTimer(main.Clock.Time));
        _face.Bind(ClockFace.HourAngleProperty, new Binding(nameof(AnalogClockViewModel.HourAngle)) { Source = _clock });
        _face.Bind(ClockFace.MinuteAngleProperty, new Binding(nameof(AnalogClockViewModel.MinuteAngle)) { Source = _clock });
        _face.Bind(ClockFace.SecondAngleProperty, new Binding(nameof(AnalogClockViewModel.SecondAngle)) { Source = _clock });
        _clock.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _clock?.Detach();
        _clock = null;
        base.OnDetachedFromVisualTree(e);
    }
}
