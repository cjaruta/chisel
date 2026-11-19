using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// The analog clock widget: just a face, following MainViewModel.AnalogClock while it is on screen. The face
// ignores the mouse so the whole widget stays a drag handle in edit mode. As a hidden tab of a group it stops
// following the clock (and so stops redrawing) until it is shown again.
public sealed class ClockWidgetView : WidgetView
{
    private readonly ClockFace _face = new() { IsHitTestVisible = false };
    private IDisposable[] _bindings = [];
    private MainViewModel? _main;

    public ClockWidgetView()
    {
        // The heading sits 12px above this view's top (header margin and its spare height). Pad the bottom by the
        // same amount so the face has equal space above and below.
        Padding = new Thickness(0, 0, 0, 12);
        Content = _face;
    }

    // Whether the face is following the clock (it is while the widget is shown).
    public bool IsFollowingClock => _bindings.Length > 0;

    // With no title bar the face is centered in the whole card, so the bottom padding goes.
    protected override void OnHeaderShownChanged() => Padding = HeaderShown ? new Thickness(0, 0, 0, 12) : new Thickness(0);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _main = DataContext as MainViewModel;
        Follow(IsEffectivelyVisible);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Follow(false);
        _main = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_main is not null && (change.Property == IsVisibleProperty || change.Property.Name == nameof(IsEffectivelyVisible)))
        {
            Follow(IsEffectivelyVisible);
        }
    }

    private void Follow(bool on)
    {
        if (on && _main is { } main && _bindings.Length == 0)
        {
            _bindings =
            [
                _face.Bind(ClockFace.HourAngleProperty, new Binding(nameof(AnalogClockViewModel.HourAngle)) { Source = main.AnalogClock }),
                _face.Bind(ClockFace.MinuteAngleProperty, new Binding(nameof(AnalogClockViewModel.MinuteAngle)) { Source = main.AnalogClock }),
                _face.Bind(ClockFace.SecondAngleProperty, new Binding(nameof(AnalogClockViewModel.SecondAngle)) { Source = main.AnalogClock }),
            ];
        }
        else if (!on)
        {
            foreach (var binding in _bindings)
            {
                binding.Dispose();
            }
            _bindings = [];
        }
    }
}
