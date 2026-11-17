using System;
using Avalonia;
using Avalonia.Controls;

namespace Chisel.App.Views.Widgets;

// Base for a widget's content view. HeaderActions are controls the frame shows next to the widget's heading
// (like the schedule's Now / + Marker / + Block buttons). The DataContext is the MainViewModel.
public class WidgetView : UserControl
{
    public static readonly StyledProperty<Control?> HeaderActionsProperty =
        AvaloniaProperty.Register<WidgetView, Control?>(nameof(HeaderActions));

    public Control? HeaderActions
    {
        get => GetValue(HeaderActionsProperty);
        set => SetValue(HeaderActionsProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(UserControl);
}
