using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Chisel.App.Views.Widgets;

// Base for a widget's content view. BodyActions are buttons the frame shows in a slim row at the top of the body,
// whether or not the header is shown (like the schedule's Now / + Marker / + Block). The header itself is the
// frame's and holds only the title. The DataContext is the MainViewModel.
public class WidgetView : UserControl
{
    public static readonly StyledProperty<Control?> BodyActionsProperty =
        AvaloniaProperty.Register<WidgetView, Control?>(nameof(BodyActions));

    public Control? BodyActions
    {
        get => GetValue(BodyActionsProperty);
        set => SetValue(BodyActionsProperty, value);
    }

    // Whether the card's title bar is shown (set by the frame). Views that adapt to it override OnHeaderShownChanged.
    public bool HeaderShown { get; private set; } = true;

    public void SetHeaderShown(bool shown)
    {
        if (HeaderShown != shown)
        {
            HeaderShown = shown;
            OnHeaderShownChanged();
        }
    }

    protected virtual void OnHeaderShownChanged() { }

    protected override Type StyleKeyOverride => typeof(UserControl);
}
