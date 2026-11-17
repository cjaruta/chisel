using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// The card around one widget: its heading (with the widget's header actions on the right) and its content,
// scrolled inside the card when the widget asks for it. Created once per widget and reused when the layout changes.
//
// In layout edit mode the card gets a neutral hairline, a soft shadow and a slight scale-down (render
// transform), and its content is dimmed and ignores the mouse and keyboard. A transparent layer over the
// whole card is the drag handle, with a round remove badge on the top-left corner. The border's space is
// always reserved (transparent outside edit mode), so nothing moves.
public sealed class WidgetFrame : Border
{
    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<WidgetFrame, bool>(nameof(IsEditing));

    // Six dots in two columns: the "drag me" handle.
    public const string GripData = "M0,0 h3 v3 h-3 Z M6,0 h3 v3 h-3 Z M0,6 h3 v3 h-3 Z M6,6 h3 v3 h-3 Z M0,12 h3 v3 h-3 Z M6,12 h3 v3 h-3 Z";

    private readonly Grid _editLayer;

    public WidgetFrame(WidgetDefinition widget, WidgetView view)
    {
        Widget = widget;
        View = view;
        Classes.Add("card");
        Classes.Add("widgetFrame");
        MinHeight = widget.MinHeight;

        var heading = new TextBlock { Text = widget.Title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0) };
        heading.Classes.Add("cardHeading");
        heading.Classes.Add("widgetHeading");
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Classes.Add("widgetHeader");
        header.Children.Add(heading);
        if (view.HeaderActions is { } actions)
        {
            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);
        }
        DockPanel.SetDock(header, Dock.Top);

        Control body = view;
        if (widget.ScrollsContent)
        {
            view.Classes.Add("scrolledWidget");
            body = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = view,
            };
        }

        Content = new DockPanel { Children = { header, body } };
        Content.Classes.Add("widgetContent");

        // Takes the widget off the dashboard (into the tray); its data stays. A small badge on the top-left corner.
        RemoveButton = new Button
        {
            Name = $"RemoveWidget_{widget.Id}",
            Content = new Path { Data = Geometry.Parse("M0,0 L6,0") },
        };
        RemoveButton.Classes.Add("removeWidget");
        ToolTip.SetTip(RemoveButton, "Remove from dashboard");

        // The whole widget is the drag handle. Transparent so it catches every click and hover meant for the
        // content underneath; focusable so focus can be moved off the content when editing starts.
        EditBar = new Border { Background = Brushes.Transparent, Focusable = true };
        EditBar.Classes.Add("editBar");
        _editLayer = new Grid { IsVisible = false, Children = { EditBar, RemoveButton } };
        _editLayer.Classes.Add("editLayer");

        Child = new Panel { Children = { Content, _editLayer } };
    }

    public WidgetDefinition Widget { get; }
    public string WidgetId => Widget.Id;
    public WidgetView View { get; }

    // The heading and the widget's view; inert and dimmed in edit mode.
    public DockPanel Content { get; }

    // The transparent layer over the whole card that drags the widget in edit mode.
    public Border EditBar { get; }

    // The corner badge that removes the widget from the dashboard.
    public Button RemoveButton { get; }

    public bool IsEditing
    {
        get => GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    // Styled as a plain Border, so the shared card look ("Border.card") applies.
    protected override System.Type StyleKeyOverride => typeof(Border);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEditingProperty)
        {
            var editing = change.GetNewValue<bool>();
            Classes.Set("editing", editing);
            _editLayer.IsVisible = editing;

            // Inert without the theme's disabled look: no hit testing, no tabbing in, and no focus left inside.
            Content.IsHitTestVisible = !editing;
            KeyboardNavigation.SetTabNavigation(Content, editing ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
            var focus = TopLevel.GetTopLevel(this)?.FocusManager;
            if (editing && focus?.GetFocusedElement() is Visual focused && Content.IsVisualAncestorOf(focused))
            {
                EditBar.Focus();
            }
        }
    }
}
