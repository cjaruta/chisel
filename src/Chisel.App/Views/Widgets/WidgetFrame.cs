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
using Chisel.Core.Layout;

namespace Chisel.App.Views.Widgets;

// The card around one widget, or around a group of widgets: a header and the body below it, scrolled
// inside the card when the widget asks for it. Created once per card id and reused when the layout changes.
//
// The header is the one shared bar (WidgetFrame.Header). For one widget it holds the title; for a group in tabs view
// it holds the members' titles as tabs (active: accent text and underline; inactive: secondary text) and is always
// shown. The body holds every member's body, but only the active one is visible: the others stay in the tree (so
// their data, drafts and scroll positions are kept) yet are not shown, so they are not measured or drawn and every
// view that checks IsEffectivelyVisible (timers, animation, audio capture) is idle.
//
// A group can also be shown in split view: the members side by side as separate rounded panes, each with its own
// header of the same height, background and padding, and a thin splitter in each gap. Every pane is visible and live.
// The saved mode stays Split when the card is too narrow for the panes' minimum widths; it then shows as tabs and
// goes back to split as soon as there is room.
//
// In layout edit mode the card gets a neutral hairline, a soft shadow and a slight scale-down (render
// transform), and its body is dimmed and ignores the mouse and keyboard. A transparent layer over the card is the
// drag handle (below the header on a group, so its tabs and panes' headers stay usable), with a round remove badge
// on the top-left corner. The splitters and the view toggle sit above that layer. The border's space is always
// reserved (transparent outside edit mode), so nothing moves.
public sealed class WidgetFrame : Border
{
    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<WidgetFrame, bool>(nameof(IsEditing));

    // Six dots in two columns: the "drag me" handle.
    public const string GripData = "M0,0 h3 v3 h-3 Z M6,0 h3 v3 h-3 Z M0,6 h3 v3 h-3 Z M6,6 h3 v3 h-3 Z M0,12 h3 v3 h-3 Z M6,12 h3 v3 h-3 Z";

    // The header's own leading column for the remove badge: zero wide, opened in edit mode so the title never sits under it.
    private readonly ColumnDefinition _badgeColumn;
    private ColumnDefinition? _paneBadgeColumn;
    private const double BadgeColumnWidth = 44;

    // The one header every card shares: a fixed-height bar with the title (or the tabs) on the left.
    public const double HeaderHeight = 44;

    // How far a pressed tab must move before it is a drag (not a click).
    private const double TabDragThreshold = 4;

    // How far one arrow key press moves a splitter (with Shift, three times as far).
    private const double SplitterKeyStep = 16;

    private readonly Grid _editLayer;
    private readonly Grid _headerGrid;
    private readonly TextBlock _heading;
    private readonly Grid _tabStrip = new();
    private readonly Panel _bodyHost = new();
    private readonly DockPanel _dock;
    private readonly Grid _splitGrid = new();
    private readonly Grid _splitOverlay = new();
    private readonly List<Member> _members = new();
    private readonly List<Control> _splitters = new();
    private int _active;
    private bool _showHeader = true;

    // The view mode the layout asks for, the panes' width weights, and whether the panes are what is shown now.
    private GroupViewMode _mode = GroupViewMode.Tabs;
    private IReadOnlyList<double> _weights = [];
    private bool _splitShown;

    // Set when the card is off the dashboard: its bodies went to other cards, so it must not touch the views.
    private bool _released;
    private IEnumerable<Member> Live => _released ? [] : _members;

    // Tabs: which widget the user picked, wants moved out of the group, or moved to a new place (index).
    public event Action<string>? TabSelected;
    public event Action<string>? TabUnmerged;
    public event Action<string, int>? TabReordered;

    // The view toggle: the mode the user asked for and the card's width in pixels.
    public event Action<GroupViewMode, double>? ViewModeRequested;

    // A splitter drag: it began; moved the pane (by id) by dx pixels against the next one (each pane's minimum width and
    // the width all panes share are given in pixels); ended (committed, or cancelled).
    public event Action? PaneResizeBegan;
    public event Action<string, double, double, double, double>? PaneResizeMoved;
    public event Action<bool>? PaneResizeEnded;

    private sealed class Member(WidgetDefinition definition, WidgetView view, Control body)
    {
        public WidgetDefinition Definition { get; } = definition;
        public WidgetView View { get; } = view;
        public Control Body { get; } = body;
        public Grid? Tab { get; set; }
        public Button TabButton { get; set; } = null!;
        public Button UnmergeButton { get; set; } = null!;
        public Border? Pane { get; set; }
        public Border? PaneHeader { get; set; }
        public Panel? PaneBody { get; set; }
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
        _headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("0,*,Auto") };
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

        // Starts adjusting (panning and zooming) the picture a picture frame shows. Only on a picture frame, in edit mode.
        AdjustButton = new Button
        {
            Name = $"AdjustPicture_{widget.Id}",
            Content = new Path { Data = Geometry.Parse("M0,2.5 H1.5 V0 M3.5,0 V1.5 H6 M6,3.5 H4.5 V6 M2.5,6 V4.5 H0") },
            IsVisible = false,
        };
        AdjustButton.Classes.Add("removeWidget");
        AdjustButton.Classes.Add("adjustBadge");
        ToolTip.SetTip(AdjustButton, "Adjust picture");
        AutomationProperties.SetName(AdjustButton, "Adjust picture");
        AdjustButton.Click += (_, _) => AdjustRequested?.Invoke(ActiveId);

        // Switches a group between tabs and split view: at the right end of the header in tabs view, in the gap
        // between the first two panes in split view. Quiet; theme tokens only (see App.axaml).
        ViewModeButton = new Button
        {
            Name = $"ViewMode_{widget.Id}",
            Content = new Path(),
            IsVisible = false,
        };
        ViewModeButton.Classes.Add("viewModeToggle");
        ToolTip.SetShowOnDisabled(ViewModeButton, true);
        ViewModeButton.Click += (_, _) =>
        {
            if (ViewModeButton.IsEnabled)
            {
                ViewModeRequested?.Invoke(_splitShown ? GroupViewMode.Tabs : GroupViewMode.Split, InnerWidth);
            }
        };

        // The whole widget is the drag handle. Transparent so it catches every click and hover meant for the
        // content underneath; focusable so focus can be moved off the content when editing starts.
        EditBar = new Border { Background = Brushes.Transparent, Focusable = true };
        EditBar.Classes.Add("editBar");
        _editLayer = new Grid { IsVisible = false, Children = { EditBar, RemoveButton, HeaderButton, AdjustButton } };
        _editLayer.Classes.Add("editLayer");

        _splitOverlay.IsVisible = false;
        Child = new Panel { Children = { Content, _editLayer, _splitOverlay } };
        SizeChanged += (_, e) =>
        {
            if (e.WidthChanged)
            {
                Refit();
            }
        };
        SetMembers(new[] { (widget, view) }, 0);
    }

    // The badge that shows or hides the title. Not offered on a group: its header is always shown.
    public Button HeaderButton { get; }

    // The badge that starts adjusting a picture frame's picture, and the request it makes (with the frame's id).
    public Button AdjustButton { get; }

    public event System.Action<string>? AdjustRequested;

    // Whether the picture shown is being adjusted: the card stops being a drag handle and its content comes alive.
    public bool IsAdjusting
    {
        get => _adjusting;
        set
        {
            if (_adjusting != value)
            {
                _adjusting = value;
                Classes.Set("adjusting", value);
                ApplyEditing();
                UpdateAdjust();
            }
        }
    }

    private bool _adjusting;

    private bool IsPictureActive => _members.Count > 0 && _members[_active].Definition.TypeId == WidgetRegistry.PictureFrame;

    private double BadgeWidth() => IsPictureActive ? BadgeColumnWidth + 20 : BadgeColumnWidth;

    // The adjust badge shows on a picture frame (plain, or the active tab), in edit mode; adjusting hides every badge and the drag layer.
    private void UpdateAdjust()
    {
        AdjustButton.IsVisible = IsEditing && !_adjusting && IsPictureActive && !_splitShown;
        EditBar.IsVisible = !_adjusting;
        RemoveButton.IsVisible = !_adjusting;
        HeaderButton.IsVisible = !IsGroup && !_adjusting;
        if (IsEditing)
        {
            _badgeColumn.Width = new GridLength(BadgeWidth());
        }
    }

    // The group's tabs / split toggle. Only visible on a group.
    public Button ViewModeButton { get; }

    // The shared header bar. It is out of the visual tree while the header is hidden (and in split view, where each
    // pane has its own).
    public Border Header { get; }

    // The card's own widget: the one whose id the card goes by (a group's first widget when it was made).
    public WidgetDefinition Widget { get; }
    public string WidgetId => Widget.Id;

    // The widget shown in the body (the only one, unless this is a group).
    public WidgetView View => _members[_active].View;

    // Every widget's view on this card, in tab order.
    public IReadOnlyList<WidgetView> Views => _members.Select(m => m.View).ToList();
    public IReadOnlyList<string> MemberIds => _members.Select(m => m.Definition.Id).ToList();

    // The members' titles, in tab order (an instance's title is numbered when its type has several on the dashboard).
    public IReadOnlyList<string> MemberTitles => _members.Select(m => m.Definition.Title).ToList();
    public string ActiveId => _members[_active].Definition.Id;
    public bool IsGroup => _members.Count > 1;

    // Whether the panes are what the card shows now (the saved mode is split and there is room).
    public bool IsSplitShown => _splitShown;

    // The card's width inside its border, in pixels: what the panes (and their gaps) have to fit in.
    public double InnerWidth => Bounds.Width - BorderThickness.Left - BorderThickness.Right;

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

    // Split view: one widget's pane and its header, and the splitter right of the i-th pane.
    public Border Pane(string widgetId) => _members.Single(m => m.Definition.Id == widgetId).Pane!;
    public Border PaneHeader(string widgetId) => _members.Single(m => m.Definition.Id == widgetId).PaneHeader!;
    public Control PaneSplitter(int index) => _splitters[index];

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

    // Sets the widgets on the card (one for a plain card), in tab order, which tab is active, and how a group shows
    // them (the saved mode and the panes' width weights). Does nothing more than switch tabs or follow the weights
    // when the card already shows exactly these widgets. Bodies are taken from wherever they were.
    public void SetMembers(IReadOnlyList<(WidgetDefinition Definition, WidgetView View)> members, int activeIndex,
        GroupViewMode mode = GroupViewMode.Tabs, IReadOnlyList<double>? weights = null)
    {
        activeIndex = Math.Clamp(activeIndex, 0, Math.Max(0, members.Count - 1));
        weights = weights is { Count: > 0 } && weights.Count == members.Count ? weights : members.Select(_ => 1.0).ToList();
        var same = !_released && members.Count == _members.Count
            && members.Select((m, i) => ReferenceEquals(m.View, _members[i].View) && m.Definition.Title == _members[i].Definition.Title).All(x => x);
        if (same)
        {
            var modeChanged = mode != _mode;
            _mode = mode;
            _weights = weights;
            if (modeChanged)
            {
                Refit();
            }
            Activate(activeIndex);
            ApplyWeights();
            UpdateToggle();
            return;
        }

        foreach (var member in _members)
        {
            (member.Body.Parent as Panel)?.Children.Remove(member.Body);
        }
        _members.Clear();
        _released = false;
        foreach (var (definition, view) in members)
        {
            var body = Bodies.GetValue(view, v => BuildBody(definition, v));
            (body.Parent as Panel)?.Children.Remove(body);
            _members.Add(new Member(definition, view, body));
        }

        _mode = mode;
        _weights = weights;
        _active = activeIndex;
        MinHeight = members.Max(m => m.Definition.MinHeight);
        Render();
    }

    // Lets go of every body (this card is not on the dashboard), so another card can take them.
    public void Release()
    {
        _bodyHost.Children.Clear();
        _splitGrid.Children.Clear();
        foreach (var member in _members)
        {
            (member.Body.Parent as Panel)?.Children.Remove(member.Body);
        }
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

    // Whether the saved mode can be shown as panes at this width.
    private bool FitsSplit =>
        IsGroup && _mode == GroupViewMode.Split && InnerWidth > 0
        && GroupSplit.Check(_members.Select(m => m.Definition.MinWidth).ToList(), InnerWidth).Allowed;

    // The card got a new width: switch between panes and tabs if it now fits (or no longer does). Returns whether it did.
    private bool Refit()
    {
        if (_released)
        {
            return false;
        }
        if (FitsSplit != _splitShown)
        {
            Render();
            return true;
        }
        UpdateToggle();
        return false;
    }

    // Builds what the card shows: tabs (or the title) over one body, or panes side by side.
    private void Render()
    {
        _splitShown = FitsSplit;
        _bodyHost.Children.Clear();
        _splitGrid.Children.Clear();
        _splitOverlay.Children.Clear();
        _dock.Children.Clear();
        _splitters.Clear();
        foreach (var member in _members)
        {
            (member.Body.Parent as Panel)?.Children.Remove(member.Body);
            member.Pane = null;
            member.PaneHeader = null;
            member.PaneBody = null;
        }
        Classes.Set("splitFrame", _splitShown);
        _splitOverlay.IsVisible = _splitShown;

        if (_splitShown)
        {
            BuildSplit();
            _dock.Children.Add(_splitGrid);
        }
        else
        {
            foreach (var member in _members.Where(_ => !_released))
            {
                _bodyHost.Children.Add(member.Body);
            }
            BuildHeaderContent();
            _dock.Children.Add(_bodyHost);
        }

        ApplyHeader();
        ApplyActive();
        ApplyEditing();
        UpdateToggle();
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

            var unmerge = MakeUnmerge(member);
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

    // The small arrow in edit mode that moves a widget back out of its group (on its tab, or on its pane's header).
    private Button MakeUnmerge(Member member)
    {
        var id = member.Definition.Id;
        var unmerge = new Button
        {
            Name = $"Unmerge_{id}",
            Content = new Path { Data = Geometry.Parse("M1,6 L6,1 M2,1 H6 V5") },
            IsVisible = IsEditing,
        };
        unmerge.Classes.Add("tabUnmerge");
        ToolTip.SetTip(unmerge, $"Move {member.Definition.Title} out of the group");
        AutomationProperties.SetName(unmerge, $"Move {member.Definition.Title} out of the group");
        unmerge.Click += (_, _) => TabUnmerged?.Invoke(id);
        return unmerge;
    }

    // Panes side by side: a column per pane (star width by its weight, never under its minimum) with a fixed gap
    // between, and a splitter in each gap (on the overlay, above the drag layer).
    private void BuildSplit()
    {
        Classes.Set("groupFrame", true);
        ToolTip.SetTip(EditBar, string.Join(" + ", _members.Select(m => m.Definition.Title)));
        _splitGrid.ColumnDefinitions.Clear();
        _splitOverlay.ColumnDefinitions.Clear();
        _paneBadgeColumn = null;

        for (var i = 0; i < _members.Count && !_released; i++)
        {
            var member = _members[i];
            var id = member.Definition.Id;
            var star = new GridLength(Math.Max(_weights[i], 0.0001), GridUnitType.Star);
            _splitGrid.ColumnDefinitions.Add(new ColumnDefinition(star) { MinWidth = member.Definition.MinWidth });
            _splitOverlay.ColumnDefinitions.Add(new ColumnDefinition(star) { MinWidth = member.Definition.MinWidth });

            var heading = new TextBlock
            {
                Text = member.Definition.Title,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            heading.Classes.Add("cardHeading");
            heading.Classes.Add("widgetHeading");
            var unmerge = MakeUnmerge(member);
            unmerge.HorizontalAlignment = HorizontalAlignment.Right;
            var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("0,*,Auto") };
            if (i == 0)
            {
                _paneBadgeColumn = headerGrid.ColumnDefinitions[0];
                _paneBadgeColumn.Width = new GridLength(IsEditing ? BadgeColumnWidth : 0);
            }
            Grid.SetColumn(heading, 1);
            Grid.SetColumn(unmerge, 2);
            headerGrid.Children.Add(heading);
            headerGrid.Children.Add(unmerge);
            var header = new Border { Name = $"PaneHeader_{id}", Child = headerGrid, Height = HeaderHeight };
            header.Classes.Add("widgetHeader");
            DockPanel.SetDock(header, Dock.Top);

            var body = new Panel { Children = { member.Body } };
            body.Classes.Add("widgetBody");
            var pane = new Border { Name = $"Pane_{id}", Child = new DockPanel { Children = { header, body } } };
            pane.Classes.Add("splitPane");
            AutomationProperties.SetName(pane, member.Definition.Title);
            Grid.SetColumn(pane, 2 * i);
            _splitGrid.Children.Add(pane);

            member.Pane = pane;
            member.PaneHeader = header;
            member.PaneBody = body;
            member.UnmergeButton = unmerge;
            member.Tab = null;

            if (i + 1 < _members.Count)
            {
                var gap = new ColumnDefinition(GroupSplit.Gap, GridUnitType.Pixel);
                _splitGrid.ColumnDefinitions.Add(gap);
                _splitOverlay.ColumnDefinitions.Add(new ColumnDefinition(GroupSplit.Gap, GridUnitType.Pixel));
                var splitter = MakeSplitter(i);
                Grid.SetColumn(splitter, 2 * i + 1);
                _splitOverlay.Children.Add(splitter);
                _splitters.Add(splitter);
            }
        }
    }

    // A thin draggable divider in the gap right of pane i. Starts below the header row, where the toggle sits.
    private Control MakeSplitter(int index)
    {
        var left = _members[index].Definition;
        var right = _members[index + 1].Definition;
        var grip = new Border { IsHitTestVisible = false };
        grip.Classes.Add("paneSplitterGrip");
        var splitter = new Border
        {
            Name = $"PaneSplitter_{WidgetId}_{index}",
            Background = Brushes.Transparent,
            Child = grip,
            Focusable = true,
            IsTabStop = true,
            Margin = new Thickness(0, HeaderHeight, 0, 0),
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
        };
        splitter.Classes.Add("paneSplitter");
        AutomationProperties.SetName(splitter, $"Resize {left.Title} and {right.Title}");
        ToolTip.SetTip(splitter, "Drag to resize");

        splitter.PointerPressed += (_, e) => OnSplitterPressed(index, splitter, e);
        splitter.PointerMoved += (_, e) => OnSplitterMoved(index, e);
        splitter.PointerReleased += (_, e) => OnSplitterReleased(splitter, e);
        splitter.PointerCaptureLost += (_, _) => EndSplitterDrag(commit: false);
        splitter.KeyDown += (_, e) => OnSplitterKey(index, e);
        return splitter;
    }

    // The pixels all panes share (the card's width minus the gaps) and the two neighbours' minimum widths.
    private (double Space, double LeftMin, double RightMin) PaneMeasures(int index) =>
        (Math.Max(1, _splitGrid.Bounds.Width - GroupSplit.Gap * (_members.Count - 1)),
            _members[index].Definition.MinWidth, _members[index + 1].Definition.MinWidth);

    private int? _splitterDrag;
    private double _splitterStartX;

    private void OnSplitterPressed(int index, Control splitter, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _splitterDrag = index;
        _splitterStartX = e.GetPosition(this).X;
        e.Pointer.Capture(splitter);
        splitter.Classes.Set("dragging", true);
        splitter.Focus();
        PaneResizeBegan?.Invoke();
        e.Handled = true;
    }

    private void OnSplitterMoved(int index, PointerEventArgs e)
    {
        if (_splitterDrag != index)
        {
            return;
        }
        var (space, leftMin, rightMin) = PaneMeasures(index);
        PaneResizeMoved?.Invoke(_members[index].Definition.Id, e.GetPosition(this).X - _splitterStartX, leftMin, rightMin, space);
        e.Handled = true;
    }

    private void OnSplitterReleased(Control splitter, PointerReleasedEventArgs e)
    {
        if (_splitterDrag is null)
        {
            return;
        }
        EndSplitterDrag(commit: true);
        if (e.Pointer.Captured == splitter)
        {
            e.Pointer.Capture(null);
        }
        e.Handled = true;
    }

    private void EndSplitterDrag(bool commit)
    {
        if (_splitterDrag is null)
        {
            return;
        }
        _splitterDrag = null;
        foreach (var splitter in _splitters)
        {
            splitter.Classes.Set("dragging", false);
        }
        PaneResizeEnded?.Invoke(commit);
    }

    // Arrow keys move the divider: one step, or three with Shift.
    private void OnSplitterKey(int index, KeyEventArgs e)
    {
        var direction = e.Key switch { Key.Left => -1, Key.Right => 1, _ => 0 };
        if (direction == 0)
        {
            return;
        }
        var step = SplitterKeyStep * (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 3 : 1);
        var (space, leftMin, rightMin) = PaneMeasures(index);
        PaneResizeBegan?.Invoke();
        PaneResizeMoved?.Invoke(_members[index].Definition.Id, direction * step, leftMin, rightMin, space);
        PaneResizeEnded?.Invoke(true);
        e.Handled = true;
    }

    // Follows the layout's pane weights without rebuilding anything.
    private void ApplyWeights()
    {
        if (!_splitShown || _splitGrid.ColumnDefinitions.Count != 2 * _members.Count - 1)
        {
            return;
        }
        for (var i = 0; i < _members.Count; i++)
        {
            var star = new GridLength(Math.Max(_weights[i], 0.0001), GridUnitType.Star);
            _splitGrid.ColumnDefinitions[2 * i].Width = star;
            _splitOverlay.ColumnDefinitions[2 * i].Width = star;
        }
    }

    // Puts the toggle where it belongs for what is shown, with its icon, tooltip, name and enabled state.
    private void UpdateToggle()
    {
        (ViewModeButton.Parent as Panel)?.Children.Remove(ViewModeButton);
        ViewModeButton.IsVisible = IsGroup && !_released;
        if (!ViewModeButton.IsVisible)
        {
            return;
        }

        var path = (Path)ViewModeButton.Content!;
        if (_splitShown)
        {
            // In the gap between the first two panes, level with the headers.
            Grid.SetColumn(ViewModeButton, 1);
            ViewModeButton.Width = GroupSplit.Gap;
            ViewModeButton.Height = 28;
            ViewModeButton.VerticalAlignment = VerticalAlignment.Top;
            ViewModeButton.Margin = new Thickness(0, (HeaderHeight - 28) / 2, 0, 0);
            _splitOverlay.Children.Add(ViewModeButton);
            path.Data = Geometry.Parse("M1,2 H9 M1,5 H9 M1,8 H9");
            ViewModeButton.IsEnabled = true;
            ToolTip.SetTip(ViewModeButton, "Show the widgets as tabs");
            AutomationProperties.SetName(ViewModeButton, "Tabs view");
            return;
        }

        // At the right end of the header.
        Grid.SetColumn(ViewModeButton, 2);
        ViewModeButton.Width = 28;
        ViewModeButton.Height = 28;
        ViewModeButton.VerticalAlignment = VerticalAlignment.Center;
        ViewModeButton.Margin = new Thickness(4, 0, 0, 0);
        _headerGrid.Children.Add(ViewModeButton);
        path.Data = Geometry.Parse("M1,1 H5 V9 H1 Z M7,1 H11 V9 H7 Z");
        var check = GroupSplit.Check(_members.Select(m => m.Definition.MinWidth).ToList(), InnerWidth);
        ViewModeButton.IsEnabled = check.Allowed;
        ToolTip.SetTip(ViewModeButton, check.Allowed ? "Show the widgets side by side" : check.Reason);
        AutomationProperties.SetName(ViewModeButton, "Split view");
    }

    private void Activate(int index)
    {
        if (index != _active && index >= 0 && index < _members.Count)
        {
            _active = index;
            ApplyActive();
        }
    }

    // In tabs view only the active body is shown; the others stay in the tree, hidden, and so idle. In split view
    // every pane is shown (and live).
    private void ApplyActive()
    {
        UpdateAdjust();
        for (var i = 0; i < _members.Count && !_released; i++)
        {
            var member = _members[i];
            member.Body.IsVisible = member.View.IsVisible = _splitShown || i == _active;
            if (IsGroup && !_splitShown)
            {
                member.TabButton.Classes.Set("active", i == _active);
                AutomationProperties.SetItemStatus(member.TabButton, i == _active ? "Selected" : "");
            }
        }
    }

    private void ApplyHeader()
    {
        var shown = HeaderShown;
        if (!_splitShown)
        {
            if (shown && !_dock.Children.Contains(Header))
            {
                _dock.Children.Insert(0, Header);
            }
            else if (!shown)
            {
                _dock.Children.Remove(Header);
            }
        }
        foreach (var member in Live)
        {
            member.View.Classes.Set("headerless", !shown);
            member.View.SetHeaderShown(shown);
        }
        HeaderButton.IsVisible = !IsGroup && !_adjusting;
        ToolTip.SetTip(HeaderButton, _showHeader ? "Hide title" : "Show title");
    }

    // Edit mode: the body is inert and dimmed (the whole card, on a plain widget); a group keeps its header live
    // for its tabs (or panes' headers), and its drag layer starts below the header.
    private void ApplyEditing()
    {
        var editing = IsEditing;
        var inert = editing && !IsGroup && !_adjusting;
        Content.IsHitTestVisible = !inert;
        _bodyHost.IsHitTestVisible = !editing || _adjusting;
        var mode = editing ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue;
        KeyboardNavigation.SetTabNavigation(_bodyHost, mode);
        foreach (var body in Live.Select(m => m.PaneBody).OfType<Panel>())
        {
            body.IsHitTestVisible = !editing;
            KeyboardNavigation.SetTabNavigation(body, mode);
        }
        if (_paneBadgeColumn is not null)
        {
            _paneBadgeColumn.Width = new GridLength(editing ? BadgeColumnWidth : 0);
        }
        KeyboardNavigation.SetTabNavigation(Content, inert ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        EditBar.Margin = IsGroup ? new Thickness(0, HeaderHeight, 0, 0) : new Thickness(0);
        foreach (var member in Live.Where(m => m.UnmergeButton is not null))
        {
            member.UnmergeButton.IsVisible = editing && IsGroup;
        }

        var focus = TopLevel.GetTopLevel(this)?.FocusManager;
        var bodies = new List<Visual>();
        if (inert)
        {
            bodies.Add(Content);
        }
        else if (_splitShown)
        {
            bodies.AddRange(Live.Select(m => m.PaneBody).OfType<Visual>());
        }
        else
        {
            bodies.Add(_bodyHost);
        }
        if (editing && focus?.GetFocusedElement() is Visual focused && bodies.Any(b => b.IsVisualAncestorOf(focused)))
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
            _members[i].Tab?.Classes.Set("tabDragging", i == from);
            _members[i].Tab?.Classes.Set("tabDropTarget", i == _tabDropIndex && i != from);
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
            member.Tab!.Classes.Set("tabDragging", false);
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
            _badgeColumn.Width = new GridLength(editing ? BadgeWidth() : 0);
            ApplyEditing();
            UpdateAdjust();
        }
    }
}
