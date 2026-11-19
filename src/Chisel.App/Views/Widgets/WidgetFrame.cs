using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// The card around one widget, or around a tabbed group of widgets: a header and the body below it, scrolled
// inside the card when the widget asks for it. Created once per card id and reused when the layout changes.
//
// The header is the one shared bar (WidgetFrame.Header). For one widget it holds the title; for a group it holds
// the members' titles as tabs (active: accent text and underline; inactive: secondary text) and is always shown.
// The body holds every member's body, but only the active one is visible: the others stay in the tree (so their
// data, drafts and scroll positions are kept) yet are not shown, so they are not measured or drawn and every view
// that checks IsEffectivelyVisible (timers, animation, audio capture) is idle.
//
// In layout edit mode the card gets a neutral hairline, a soft shadow and a slight scale-down (render
// transform), and its body is dimmed and ignores the mouse and keyboard. A transparent layer over the card is the
// drag handle (below the header on a group, so its tabs stay usable), with a round remove badge on the top-left
// corner. The border's space is always reserved (transparent outside edit mode), so nothing moves.
public sealed class WidgetFrame : Border
{
    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<WidgetFrame, bool>(nameof(IsEditing));

    // Six dots in two columns: the "drag me" handle.
    public const string GripData = "M0,0 h3 v3 h-3 Z M6,0 h3 v3 h-3 Z M0,6 h3 v3 h-3 Z M6,6 h3 v3 h-3 Z M0,12 h3 v3 h-3 Z M6,12 h3 v3 h-3 Z";

    // The header's own leading column for the remove badge: zero wide, opened in edit mode so the title never sits under it.
    private readonly ColumnDefinition _badgeColumn;
    private const double BadgeColumnWidth = 44;

    // The one header every card shares: a fixed-height bar with the title (or the tabs) on the left.
    public const double HeaderHeight = 44;

    // How far a pressed tab must move before it is a drag (not a click).
    private const double TabDragThreshold = 4;

    private readonly Grid _editLayer;
    private readonly Grid _headerGrid;
    private readonly TextBlock _heading;
    private readonly Grid _tabStrip = new();
    private readonly Panel _bodyHost = new();
    private readonly DockPanel _dock;
    private readonly List<Member> _members = new();
    private int _active;
    private bool _showHeader = true;

    // Set when the card is off the dashboard: its bodies went to other cards, so it must not touch the views.
    private bool _released;
    private IEnumerable<Member> Live => _released ? [] : _members;

    // Tabs: which widget the user picked, wants moved out of the group, or moved to a new place (index).
    public event Action<string>? TabSelected;
    public event Action<string>? TabUnmerged;
    public event Action<string, int>? TabReordered;

    private sealed class Member(WidgetDefinition definition, WidgetView view, Control body)
    {
        public WidgetDefinition Definition { get; } = definition;
        public WidgetView View { get; } = view;
        public Control Body { get; } = body;
        public Grid Tab { get; set; } = null!;
        public Button TabButton { get; set; } = null!;
        public Button UnmergeButton { get; set; } = null!;
    }

    // Each view's body (the view, its scroller and its actions row) is built once, and moves between frames.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WidgetView, Control> Bodies = new();

    public WidgetFrame(WidgetDefinition widget, WidgetView view)
    {
        Widget = widget;
        Classes.Add("card");
        Classes.Add("widgetFrame");

        _heading = new TextBlock { Text = widget.Title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0) };
        _heading.Classes.Add("cardHeading");
        _heading.Classes.Add("widgetHeading");
        _headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("0,*") };
        _badgeColumn = _headerGrid.ColumnDefinitions[0];
        Grid.SetColumn(_heading, 1);
        _headerGrid.Children.Add(_heading);
        _tabStrip.Name = $"GroupTabs_{widget.Id}";
        _tabStrip.Classes.Add("groupTabs");
        Grid.SetColumn(_tabStrip, 1);
        Header = new Border { Child = _headerGrid, Height = HeaderHeight };
        Header.Classes.Add("widgetHeader");
        DockPanel.SetDock(Header, Dock.Top);

        _bodyHost.Classes.Add("widgetBody");
        _dock = new DockPanel { Children = { Header, _bodyHost } };
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
        AutomationProperties.SetName(RemoveButton, "Remove from dashboard");

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
        _editLayer = new Grid { IsVisible = false, Children = { EditBar, RemoveButton, HeaderButton } };
        _editLayer.Classes.Add("editLayer");

        Child = new Panel { Children = { Content, _editLayer } };
        SetMembers(new[] { (widget, view) }, 0);
    }

    // The badge that shows or hides the title. Not offered on a group: its header is always shown.
    public Button HeaderButton { get; }

    // The shared header bar. It is out of the visual tree while the header is hidden.
    public Border Header { get; }

    // The card's own widget: the one whose id the card goes by (a group's first widget when it was made).
    public WidgetDefinition Widget { get; }
    public string WidgetId => Widget.Id;

    // The widget shown in the body (the only one, unless this is a group).
    public WidgetView View => _members[_active].View;

    // Every widget's view on this card, in tab order.
    public IReadOnlyList<WidgetView> Views => _members.Select(m => m.View).ToList();
    public IReadOnlyList<string> MemberIds => _members.Select(m => m.Definition.Id).ToList();
    public string ActiveId => _members[_active].Definition.Id;
    public bool IsGroup => _members.Count > 1;

    // The heading and the widget's body; inert and dimmed in edit mode (on a group only the body is).
    public Panel Content { get; }

    // The transparent layer over the card that drags the widget in edit mode.
    public Border EditBar { get; }

    // The corner badge that removes the widget from the dashboard.
    public Button RemoveButton { get; }

    // The group's tab strip, and one tab's buttons (for the host and for tests).
    public Grid TabStrip => _tabStrip;
    public Button TabButton(string widgetId) => _members.Single(m => m.Definition.Id == widgetId).TabButton;
    public Button UnmergeButton(string widgetId) => _members.Single(m => m.Definition.Id == widgetId).UnmergeButton;

    // Whether the header is shown. Hidden, the whole row is removed and the body fills the card. A group ignores
    // this: it always shows its header (the choice of each widget is kept by the layout and comes back on unmerge).
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

    public bool HeaderShown => IsGroup || _showHeader;

    // Sets the widgets on the card (one for a plain card), in tab order, and which tab is active. Does nothing
    // when the card already shows exactly this. Bodies are taken from wherever they were.
    public void SetMembers(IReadOnlyList<(WidgetDefinition Definition, WidgetView View)> members, int activeIndex)
    {
        activeIndex = Math.Clamp(activeIndex, 0, Math.Max(0, members.Count - 1));
        var same = !_released && members.Count == _members.Count
            && members.Select((m, i) => ReferenceEquals(m.View, _members[i].View)).All(x => x);
        if (same)
        {
            Activate(activeIndex);
            return;
        }

        _bodyHost.Children.Clear();
        _members.Clear();
        _released = false;
        foreach (var (definition, view) in members)
        {
            var body = Bodies.GetValue(view, v => BuildBody(definition, v));
            (body.Parent as Panel)?.Children.Remove(body);
            _bodyHost.Children.Add(body);
            _members.Add(new Member(definition, view, body));
        }

        BuildHeaderContent();
        MinHeight = members.Max(m => m.Definition.MinHeight);
        _active = activeIndex;
        ApplyHeader();
        ApplyActive();
        ApplyEditing();
    }

    // Lets go of every body (this card is not on the dashboard), so another card can take them.
    public void Release()
    {
        _bodyHost.Children.Clear();
        _released = true;
    }

    private static Control BuildBody(WidgetDefinition widget, WidgetView view)
    {
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
        return body;
    }

    // The title for one widget; the tabs for a group.
    private void BuildHeaderContent()
    {
        _headerGrid.Children.Remove(_heading);
        _headerGrid.Children.Remove(_tabStrip);
        _tabStrip.Children.Clear();
        _tabStrip.ColumnDefinitions.Clear();

        if (!IsGroup)
        {
            _heading.Text = _members[0].Definition.Title;
            _headerGrid.Children.Add(_heading);
            ToolTip.SetTip(EditBar, _members[0].Definition.Title);
            Classes.Set("groupFrame", false);
            return;
        }

        Classes.Set("groupFrame", true);
        ToolTip.SetTip(EditBar, string.Join(" + ", _members.Select(m => m.Definition.Title)));
        for (var i = 0; i < _members.Count && !_released; i++)
        {
            var member = _members[i];
            _tabStrip.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star) { MinWidth = 0 });

            var title = new TextBlock
            {
                Text = member.Definition.Title,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var button = new Button
            {
                Name = $"GroupTab_{member.Definition.Id}",
                Content = title,
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            button.Classes.Add("groupTab");
            AutomationProperties.SetName(button, member.Definition.Title);
            var id = member.Definition.Id;
            button.Click += (_, _) =>
            {
                if (!_tabDragged)
                {
                    TabSelected?.Invoke(id);
                }
                _tabDragged = false;
            };
            // The button handles its own presses, so listen even to handled events.
            button.AddHandler(PointerPressedEvent, (_, e) => OnTabPressed(id, e), RoutingStrategies.Bubble, handledEventsToo: true);
            button.AddHandler(PointerMovedEvent, (_, e) => OnTabMoved(e), RoutingStrategies.Bubble, handledEventsToo: true);
            button.AddHandler(PointerReleasedEvent, (_, e) => OnTabReleased(e), RoutingStrategies.Bubble, handledEventsToo: true);
            button.PointerCaptureLost += (_, _) => EndTabDrag();

            var unmerge = new Button
            {
                Name = $"Unmerge_{member.Definition.Id}",
                Content = new Path { Data = Geometry.Parse("M1,6 L6,1 M2,1 H6 V5") },
                IsVisible = IsEditing,
            };
            unmerge.Classes.Add("tabUnmerge");
            ToolTip.SetTip(unmerge, $"Move {member.Definition.Title} out of the group");
            AutomationProperties.SetName(unmerge, $"Move {member.Definition.Title} out of the group");
            unmerge.Click += (_, _) => TabUnmerged?.Invoke(id);

            var tab = new Grid { Children = { button, unmerge } };
            tab.Classes.Add("groupTabCell");
            Grid.SetColumn(tab, i);
            _tabStrip.Children.Add(tab);
            member.Tab = tab;
            member.TabButton = button;
            member.UnmergeButton = unmerge;
        }
        _headerGrid.Children.Add(_tabStrip);
    }

    private void Activate(int index)
    {
        if (index != _active && index >= 0 && index < _members.Count)
        {
            _active = index;
            ApplyActive();
        }
    }

    // Only the active body is shown; the others stay in the tree, hidden, and so idle.
    private void ApplyActive()
    {
        for (var i = 0; i < _members.Count && !_released; i++)
        {
            var member = _members[i];
            member.Body.IsVisible = member.View.IsVisible = i == _active;
            if (IsGroup)
            {
                member.TabButton.Classes.Set("active", i == _active);
                AutomationProperties.SetItemStatus(member.TabButton, i == _active ? "Selected" : "");
            }
        }
    }

    private void ApplyHeader()
    {
        var shown = HeaderShown;
        if (shown && !_dock.Children.Contains(Header))
        {
            _dock.Children.Insert(0, Header);
        }
        else if (!shown)
        {
            _dock.Children.Remove(Header);
        }
        foreach (var member in Live)
        {
            member.View.Classes.Set("headerless", !shown);
            member.View.SetHeaderShown(shown);
        }
        HeaderButton.IsVisible = !IsGroup;
        ToolTip.SetTip(HeaderButton, _showHeader ? "Hide title" : "Show title");
    }

    // Edit mode: the body is inert and dimmed (the whole card, on a plain widget); a group keeps its header live
    // for its tabs, and its drag layer starts below the header.
    private void ApplyEditing()
    {
        var editing = IsEditing;
        var inert = editing && !IsGroup;
        Content.IsHitTestVisible = !inert;
        _bodyHost.IsHitTestVisible = !editing;
        var mode = editing ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue;
        KeyboardNavigation.SetTabNavigation(_bodyHost, mode);
        KeyboardNavigation.SetTabNavigation(Content, inert ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        EditBar.Margin = IsGroup ? new Thickness(0, HeaderHeight, 0, 0) : new Thickness(0);
        foreach (var member in Live.Where(m => m.UnmergeButton is not null))
        {
            member.UnmergeButton.IsVisible = editing && IsGroup;
        }

        var focus = TopLevel.GetTopLevel(this)?.FocusManager;
        if (editing && focus?.GetFocusedElement() is Visual focused && (inert ? Content : _bodyHost).IsVisualAncestorOf(focused))
        {
            EditBar.Focus();
        }
    }

    // A tab pressed in edit mode may turn into a drag that reorders the tabs.
    private int? _pressedTab;
    private Point _tabPress;
    private bool _tabDragged;
    private int _tabDropIndex;

    private void OnTabPressed(string id, PointerPressedEventArgs e)
    {
        _tabDragged = false;
        if (!IsEditing || !e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressedTab = _members.FindIndex(m => m.Definition.Id == id);
        _tabPress = e.GetPosition(_tabStrip);
    }

    private void OnTabMoved(PointerEventArgs e)
    {
        if (_pressedTab is not { } from || !IsEditing)
        {
            return;
        }
        var point = e.GetPosition(_tabStrip);
        if (!_tabDragged && Math.Abs(point.X - _tabPress.X) < TabDragThreshold)
        {
            return;
        }
        _tabDragged = true;
        _tabDropIndex = Math.Clamp((int)(point.X / Math.Max(1, _tabStrip.Bounds.Width / _members.Count)), 0, _members.Count - 1);
        for (var i = 0; i < _members.Count && !_released; i++)
        {
            _members[i].Tab.Classes.Set("tabDragging", i == from);
            _members[i].Tab.Classes.Set("tabDropTarget", i == _tabDropIndex && i != from);
        }
    }

    private void OnTabReleased(PointerReleasedEventArgs e)
    {
        if (_pressedTab is { } from && _tabDragged && from != _tabDropIndex)
        {
            var id = _members[from].Definition.Id;
            var to = _tabDropIndex;
            EndTabDrag();
            TabReordered?.Invoke(id, to);
            return;
        }
        EndTabDrag(keepDragged: _tabDragged);
    }

    private void EndTabDrag(bool keepDragged = false)
    {
        _pressedTab = null;
        foreach (var member in _members.Where(m => m.Tab is not null))
        {
            member.Tab.Classes.Set("tabDragging", false);
            member.Tab.Classes.Set("tabDropTarget", false);
        }
        // The click that ends a drag is not a tab selection (Click fires after Released).
        _tabDragged = keepDragged;
    }

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
            foreach (var member in Live)
            {
                member.View.Classes.Set("editingWidget", editing);
            }
            _editLayer.IsVisible = editing;
            _badgeColumn.Width = new GridLength(editing ? BadgeColumnWidth : 0);
            ApplyEditing();
        }
    }
}
