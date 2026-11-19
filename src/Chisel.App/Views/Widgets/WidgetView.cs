using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

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

    // True when the view paints the whole card and the title bar sits on top of it (the weather backdrop).
    public virtual bool HeaderOverlaysContent => false;

    // The title's color when the view needs its own (over the weather backdrop); null keeps the theme's.
    public virtual IBrush? HeaderForeground => null;

    public event EventHandler? HeaderForegroundChanged;

    protected void RaiseHeaderForegroundChanged() => HeaderForegroundChanged?.Invoke(this, EventArgs.Empty);

    protected override Type StyleKeyOverride => typeof(UserControl);
}
