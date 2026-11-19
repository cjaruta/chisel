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

    // The header's own leading column for the remove badge: zero wide, opened in edit mode so the title never sits under it.
    private readonly ColumnDefinition _badgeColumn;
    private const double BadgeColumnWidth = 44;

    // The title bar's height: its top margin (12), minimum height (32) and bottom margin (6).
    public const double HeaderHeight = 50;

    private readonly TextBlock _heading;
    private readonly Grid _header;
    private bool _showHeader = true;

    public WidgetFrame(WidgetDefinition widget, WidgetView view)
    {
        Widget = widget;
        View = view;
        Classes.Add("card");
        Classes.Add("widgetFrame");
        MinHeight = widget.MinHeight;

        _heading = new TextBlock { Text = widget.Title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0) };
        _heading.Classes.Add("cardHeading");
        _heading.Classes.Add("widgetHeading");
        var header = _header = new Grid { ColumnDefinitions = new ColumnDefinitions("0,*,Auto") };
        _badgeColumn = header.ColumnDefinitions[0];
        header.Classes.Add("widgetHeader");
        Grid.SetColumn(_heading, 1);
        header.Children.Add(_heading);
        if (view.HeaderActions is { } actions)
        {
            Grid.SetColumn(actions, 2);
            header.Children.Add(actions);
        }
        view.HeaderForegroundChanged += (_, _) => UpdateHeadingColor();
        UpdateHeadingColor();

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

        if (view.HeaderOverlaysContent)
        {
            // The view paints the whole card; the title bar sits on top of it.
            header.VerticalAlignment = VerticalAlignment.Top;
            Content = new Panel { Children = { body, header } };
        }
        else
        {
            DockPanel.SetDock(header, Dock.Top);
            Content = new DockPanel { Children = { header, body } };
        }
        Content.Classes.Add("widgetContent");

        // Takes the widget off the dashboard (into the tray); its data stays. A small badge on the top-left corner.
        RemoveButton = new Button
        {
            Name = $"RemoveWidget_{widget.Id}",
            Content = new Path { Data = Geometry.Parse("M0,0 L6,0") },
        };
        RemoveButton.Classes.Add("removeWidget");
        ToolTip.SetTip(RemoveButton, "Remove from dashboard");

        // Shows or hides this widget's title. A small badge beside the remove badge.
        HeaderButton = new Button
        {
            Name = $"ToggleHeader_{widget.Id}",
            Content = new Path { Data = Geometry.Parse("M0,0 L6,0 M0,3.5 L4,3.5") },
        };
        HeaderButton.Classes.Add("removeWidget");
        HeaderButton.Classes.Add("headerBadge");

        // The whole widget is the drag handle. Transparent so it catches every click and hover meant for the
        // content underneath; focusable so focus can be moved off the content when editing starts.
        EditBar = new Border { Background = Brushes.Transparent, Focusable = true };
        EditBar.Classes.Add("editBar");
        ToolTip.SetTip(EditBar, widget.Title);
        _editLayer = new Grid { IsVisible = false, Children = { EditBar, RemoveButton, HeaderButton } };
        _editLayer.Classes.Add("editLayer");

        Child = new Panel { Children = { Content, _editLayer } };
        ApplyHeader();
    }

    // The badge that shows or hides the title.
    public Button HeaderButton { get; }

    // Whether the title is shown. Hidden, the row is gone (unless the widget has header buttons, which stay) and
    // the content fills the card.
    public bool ShowHeader
    {
        get => _showHeader;
        set
        {
            if (_showHeader != value)
            {
                _showHeader = value;
                ApplyHeader();
            }
        }
    }

    private void ApplyHeader()
    {
        _heading.IsVisible = _showHeader;
        _header.IsVisible = _showHeader || View.HeaderActions is not null;
        View.Classes.Set("headerless", !_showHeader);
        View.SetHeaderShown(_showHeader);
        ToolTip.SetTip(HeaderButton, _showHeader ? "Hide title" : "Show title");
    }

    private void UpdateHeadingColor()
    {
        if (View.HeaderForeground is { } brush)
        {
            _heading.Foreground = brush;
        }
        else
        {
            _heading.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    public WidgetDefinition Widget { get; }
    public string WidgetId => Widget.Id;
    public WidgetView View { get; }

    // The heading and the widget's view; inert and dimmed in edit mode.
    public Panel Content { get; }

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
            View.Classes.Set("editingWidget", editing);
            _editLayer.IsVisible = editing;
            _badgeColumn.Width = new GridLength(editing ? BadgeColumnWidth : 0);

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
