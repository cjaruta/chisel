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

    // The one header every card shares: a fixed-height bar with the title on the left.
    public const double HeaderHeight = 44;

    private readonly DockPanel _dock;
    private bool _showHeader = true;

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
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("0,*") };
        _badgeColumn = grid.ColumnDefinitions[0];
        Grid.SetColumn(heading, 1);
        grid.Children.Add(heading);
        Header = new Border { Child = grid, Height = HeaderHeight };
        Header.Classes.Add("widgetHeader");
        DockPanel.SetDock(Header, Dock.Top);

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

        if (view.BodyActions is { } actions)
        {
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            actions.Margin = new Thickness(12, 8, 12, 0);
            DockPanel.SetDock(actions, Dock.Top);
            body = new DockPanel { Children = { actions, body } };
        }
        _dock = new DockPanel { Children = { Header, body } };
        Content = _dock;
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

    // The shared header bar. It is out of the visual tree while the header is hidden.
    public Border Header { get; }

    // Whether the header is shown. Hidden, the whole row is removed and the body fills the card.
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
        if (_showHeader && !_dock.Children.Contains(Header))
        {
            _dock.Children.Insert(0, Header);
        }
        else if (!_showHeader)
        {
            _dock.Children.Remove(Header);
        }
        View.Classes.Set("headerless", !_showHeader);
        View.SetHeaderShown(_showHeader);
        ToolTip.SetTip(HeaderButton, _showHeader ? "Hide title" : "Show title");
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
