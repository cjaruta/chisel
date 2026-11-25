using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Chisel.App.Theming;
using Chisel.App.ViewModels;
using Chisel.Core.Countdown;
using Chisel.Core.Habits;

namespace Chisel.App.Views.Widgets;

// The habit tracker widget. The smallest size shows only the progress ring with the count; from there it shows a summary line and
// each habit with its checkbox; larger, each habit's streak and its last week as dots; larger still, its last five weeks as a
// small grid. What is shown and every size come from HabitLayout. "+ Add" (and clicking a habit's name) opens an inline form in
// place of the list. Rows are kept and updated in place, so a check-in never loses keyboard focus: Space toggles the focused
// habit and the arrow keys move between habits. The view does no work of its own while idle.
public sealed class HabitsWidgetView : WidgetView
{
    private static readonly CountdownColor[] Swatches = Enum.GetValues<CountdownColor>();

    // Summary size: the ring with the count inside.
    private readonly Grid _ringOnly = new() { Name = "HabitRingOnly", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly HabitRing _bigRing = new() { Name = "HabitBigRing" };
    private readonly TextBlock _count = new() { Name = "HabitCount", FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _ringLabel = new() { Name = "HabitRingLabel", HorizontalAlignment = HorizontalAlignment.Center };

    // Larger: the summary line over the rows.
    private readonly Grid _main = new() { Name = "HabitMain", RowDefinitions = new RowDefinitions("Auto,*") };
    private readonly HabitRing _smallRing = new() { Name = "HabitSummaryRing", VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _summary = new() { Name = "HabitSummary", VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly StackPanel _summaryLine = new() { Orientation = Orientation.Horizontal };
    private readonly ScrollViewer _scroll = new() { Name = "HabitScroll", HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly StackPanel _rows = new() { Name = "HabitRows" };
    private readonly List<Row> _rowViews = new();
    private readonly TextBlock _empty = new()
    {
        Name = "HabitEmpty", Text = "No habits yet.\nPress + Add to start one.", TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
    };

    // The inline form.
    private readonly ScrollViewer _form = new() { Name = "HabitForm", IsVisible = false };
    private readonly TextBox _name = new() { Name = "HabitName", Watermark = "Habit name", MaxLength = HabitValidation.MaxNameLength };
    private readonly TextBox _emoji = new() { Name = "HabitEmoji", Watermark = "Emoji (optional)", Width = 130 };
    private readonly StackPanel _swatches = new() { Name = "HabitColors", Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly StackPanel _weekdays = new() { Name = "HabitDays", Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly TextBlock _error = new() { Name = "HabitError", TextWrapping = TextWrapping.Wrap };
    private readonly Button _delete;
    private readonly Button _moveUp;
    private readonly Button _moveDown;
    private CountdownColor _formColor;

    private readonly Button _add;
    private double _addRow = 44; // the "+ Add" row's height with its margin, measured once it is shown

    private HabitsViewModel? _habits;

    public HabitLayout Layout { get; private set; } = HabitLayout.For(0, 0);

    public HabitsWidgetView()
    {
        ClipToBounds = true;
        VerticalAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "Habits");

        _add = new Button { Name = "HabitAdd", Content = "+ Add" };
        _add.Classes.Add("accent");
        _add.Click += (_, _) => _habits?.OpenAdd();
        AutomationProperties.SetName(_add, "Add a habit");
        BodyActions = _add;

        _ringOnly.Children.Add(_bigRing);
        var inside = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        inside.Children.Add(_count);
        inside.Children.Add(_ringLabel);
        _ringLabel.Classes.Add("cdDim");
        _ringOnly.Children.Add(inside);

        _summaryLine.Children.Add(_smallRing);
        _summaryLine.Children.Add(_summary);
        _summary.Classes.Add("cdDim");
        _scroll.Content = _rows;
        _rows.AddHandler(KeyDownEvent, OnRowsKeyDown, RoutingStrategies.Tunnel);
        Grid.SetRow(_scroll, 1);
        Grid.SetRow(_empty, 1);
        _empty.Classes.Add("cdDim");
        _main.Children.Add(_summaryLine);
        _main.Children.Add(_scroll);
        _main.Children.Add(_empty);

        _delete = FormButton("HabitDelete", "Delete", () => _habits?.DeleteEditing());
        _moveUp = FormButton("HabitMoveUp", "Move up", () => Move(-1));
        _moveDown = FormButton("HabitMoveDown", "Move down", () => Move(1));
        _form.Content = BuildForm();

        var root = new Panel();
        root.Children.Add(_ringOnly);
        root.Children.Add(_main);
        root.Children.Add(_form);
        Content = root;
    }

    private Control BuildForm()
    {
        foreach (var color in Swatches)
        {
            var swatch = new Button { Tag = color, Content = new Ellipse { Width = 14, Height = 14 } };
            swatch.Classes.Add("cdSwatch");
            AutomationProperties.SetName(swatch, color == CountdownColor.None ? "No color" : color.ToString());
            swatch.Click += (_, _) => ChooseColor(color);
            _swatches.Children.Add(swatch);
        }
        var culture = CultureInfo.CurrentCulture.DateTimeFormat;
        for (var i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)(((int)culture.FirstDayOfWeek + i) % 7);
            var toggle = new ToggleButton { Tag = day, Content = culture.GetShortestDayName(day), IsChecked = true };
            toggle.Classes.Add("habitDay");
            AutomationProperties.SetName(toggle, culture.GetDayName(day));
            _weekdays.Children.Add(toggle);
        }
        _error.Classes.Add("cdError");
        var save = FormButton("HabitSave", "Save", SaveForm);
        save.Classes.Add("accent");
        var cancel = FormButton("HabitCancel", "Cancel", () => _habits?.Cancel());
        var buttons = new WrapPanel();
        foreach (var b in new[] { save, cancel, _delete, _moveUp, _moveDown })
        {
            b.Margin = new Thickness(0, 0, 6, 6);
            buttons.Children.Add(b);
        }
        var marker = new WrapPanel();
        _emoji.Margin = new Thickness(0, 0, 10, 4);
        marker.Children.Add(_emoji);
        marker.Children.Add(_swatches);
        var days = new TextBlock { Text = "Due on" };
        days.Classes.Add("cdDim");
        var stack = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 4, 0) };
        foreach (var c in new Control[] { _name, marker, days, new ScrollViewer { Content = _weekdays, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }, _error, buttons })
        {
            stack.Children.Add(c);
        }
        _name.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                SaveForm();
                e.Handled = true;
            }
        };
        AutomationProperties.SetName(_name, "Habit name");
        AutomationProperties.SetName(_emoji, "Emoji, optional");
        return stack;
    }

    private static Button FormButton(string name, string text, Action click)
    {
        var button = new Button { Name = name, Content = text };
        button.Click += (_, _) => click();
        return button;
    }

    // Lifecycle --------------------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _habits = (DataContext as MainViewModel)?.Habits;
        if (_habits is not null)
        {
            _habits.PropertyChanged += OnChanged;
            Update();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_habits is not null)
        {
            _habits.PropertyChanged -= OnChanged;
            _habits = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            Update();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Update();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HabitsViewModel.IsFormOpen) && _habits is { IsFormOpen: true })
        {
            LoadForm();
        }
        // The view model raises Rebuilds last after rebuilding what is shown.
        if (e.PropertyName is nameof(HabitsViewModel.Rebuilds) or nameof(HabitsViewModel.IsFormOpen) or nameof(HabitsViewModel.FormError))
        {
            Update();
        }
    }

    // Keyboard: Up and Down move between habits (Space is the checkbox's own).
    private void OnRowsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down) || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var index = _rowViews.FindIndex(r => r.Root.IsVisible && ReferenceEquals(r.Check, focused));
        var count = _habits?.Items.Count ?? 0;
        if (index < 0 || count == 0)
        {
            return;
        }
        var next = Math.Clamp(index + (e.Key == Key.Down ? 1 : -1), 0, count - 1);
        _rowViews[next].Check.Focus(NavigationMethod.Directional);
        _rowViews[next].Root.BringIntoView();
        e.Handled = true;
    }

    // Showing ----------------------------------------------------------------------------------------------------------------

    private bool Dark => ActualThemeVariant == ThemeVariant.Dark;

    private void Update()
    {
        if (_habits is not { } habits)
        {
            return;
        }
        var form = habits.IsFormOpen;
        _form.IsVisible = form;
        _error.Text = habits.FormError ?? "";
        _error.IsVisible = habits.FormError is not null;
        _delete.IsVisible = _moveUp.IsVisible = _moveDown.IsVisible = habits.IsEditing;

        // The size is decided as if the "+ Add" row were shown; at the Summary size it is hidden to give the ring the room.
        if (_add.IsVisible && _add.Bounds.Height > 0)
        {
            _addRow = _add.Bounds.Height + _add.Margin.Top + _add.Margin.Bottom;
        }
        var height = Bounds.Height - (_add.IsVisible ? 0 : _addRow);
        var summaryOnly = HabitLayout.For(Bounds.Width, height).Detail == HabitDetail.Summary;
        _add.IsVisible = form || habits.IsEmpty || !summaryOnly;
        var layout = Layout = HabitLayout.For(Bounds.Width, Bounds.Height);
        ((Panel)Content!).Margin = new Thickness(layout.Pad);

        _count.Text = habits.CountText;
        _ringOnly.IsVisible = !form && !layout.ShowList;
        _main.IsVisible = !form && layout.ShowList;
        if (!layout.ShowList)
        {
            _ringOnly.Width = _ringOnly.Height = layout.RingSize;
            _bigRing.Fraction = habits.Fraction;
            _bigRing.AllDone = habits.AllDone;
            _count.IsVisible = !habits.AllDone;
            _count.FontSize = Math.Max(1, layout.CountFont);
            _ringLabel.IsVisible = layout.ShowRingLabel;
            _ringLabel.Text = habits.AllDone ? "All done" : habits.IsEmpty ? "No habits" : "done";
            _ringLabel.FontSize = Math.Max(1, layout.LabelFont);
            AutomationProperties.SetName(_ringOnly, habits.SummaryText);
            return;
        }

        _summaryLine.Height = layout.SummaryHeight;
        _summaryLine.Spacing = layout.Gap;
        _smallRing.Width = _smallRing.Height = layout.RingSize;
        _smallRing.Fraction = habits.Fraction;
        _smallRing.AllDone = habits.AllDone;
        _summary.Text = habits.SummaryText;
        _summary.FontSize = layout.SummaryFont;
        _summary.Classes.Set("cdDim", !habits.AllDone);
        if (habits.AllDone)
        {
            _summary.Foreground = new ImmutableSolidColorBrush(ThemeColors.CountdownTag(CountdownColor.Green, Dark));
        }
        else
        {
            _summary.ClearValue(TextBlock.ForegroundProperty);
        }
        AutomationProperties.SetName(_summaryLine, habits.SummaryText);
        _empty.IsVisible = habits.IsEmpty;
        _scroll.IsVisible = !habits.IsEmpty;

        var items = habits.Items;
        while (_rowViews.Count < items.Count)
        {
            var row = new Row(this);
            _rowViews.Add(row);
            _rows.Children.Add(row.Root);
        }
        for (var i = 0; i < _rowViews.Count; i++)
        {
            _rowViews[i].Root.IsVisible = i < items.Count;
            if (i < items.Count)
            {
                _rowViews[i].Show(items[i], habits.Today, layout, DoneBrush(items[i].Habit));
            }
        }
    }

    private IBrush DoneBrush(Habit habit) => habit.Color == CountdownColor.None
        ? ThemeService.Brush(this, "AccentBrush")
        : new ImmutableSolidColorBrush(ThemeColors.CountdownTag(habit.Color, Dark));

    // One habit's row: the checkbox, the name (with its emoji or color dot) and streak, and the history. Clicking the name edits it.
    private sealed class Row
    {
        private readonly HabitsWidgetView _view;
        private readonly Viewbox _checkBox = new() { VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _marker = new() { VerticalAlignment = VerticalAlignment.Center };
        private readonly Ellipse _dot = new() { VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _name = new() { Name = "HabitRowName", TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _streak = new() { Name = "HabitStreak", TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly Button _nameButton;
        private readonly HabitHistory _history = new() { Name = "HabitHistory", VerticalAlignment = VerticalAlignment.Center };
        private Guid _id;

        public Grid Root { get; } = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        public CheckBox Check { get; } = new() { Name = "HabitCheck" };

        public Row(HabitsWidgetView view)
        {
            _view = view;
            Check.Classes.Add("habitCheck");
            Check.Click += (_, _) => _view._habits?.Toggle(_id);
            _checkBox.Child = Check;

            var nameLine = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            var markers = new Panel();
            markers.Children.Add(_marker);
            markers.Children.Add(_dot);
            nameLine.Children.Add(markers);
            Grid.SetColumn(_name, 1);
            nameLine.Children.Add(_name);
            _name.Classes.Add("cdName");
            _streak.Classes.Add("cdDim");
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(nameLine);
            text.Children.Add(_streak);
            _nameButton = new Button { Content = text, Focusable = false };
            _nameButton.Classes.Add("cdRow");
            _nameButton.Click += (_, _) =>
            {
                if (_view.FindAncestorFrame() is not { IsEditing: true })
                {
                    _view._habits?.OpenEdit(_id);
                }
            };
            _history.DayClicked = date => _view._habits?.Toggle(_id, date);

            Grid.SetColumn(_nameButton, 1);
            Grid.SetColumn(_history, 2);
            Root.Children.Add(_checkBox);
            Root.Children.Add(_nameButton);
            Root.Children.Add(_history);
        }

        public void Show(HabitItem item, DateOnly today, HabitLayout layout, IBrush done)
        {
            _id = item.Id;
            Root.Height = layout.RowHeight;
            _checkBox.Width = _checkBox.Height = layout.CheckSize;
            Check.IsChecked = item.DoneToday;
            Check.Opacity = item.DueToday || item.DoneToday ? 1 : 0.55;
            AutomationProperties.SetName(Check, item.AutomationName);

            var habit = item.Habit;
            _marker.Text = habit.Emoji ?? "";
            _marker.IsVisible = habit.Emoji is not null;
            _marker.FontSize = layout.NameFont;
            _marker.Margin = new Thickness(0, 0, layout.Gap * 0.6, 0);
            _dot.IsVisible = habit.Emoji is null && habit.Color != CountdownColor.None;
            _dot.Width = _dot.Height = Math.Max(6, layout.NameFont * 0.55);
            _dot.Margin = new Thickness(0, 0, layout.Gap * 0.6, 0);
            _dot.Fill = done;
            _name.Text = habit.Name;
            _name.FontSize = layout.NameFont;
            _streak.IsVisible = layout.ShowStreaks;
            _streak.Text = item.StreakText;
            _streak.FontSize = layout.SmallFont;
            _nameButton.Margin = new Thickness(layout.Gap * 0.5, 0, layout.Gap * 0.5, 0);
            AutomationProperties.SetName(_nameButton, $"Edit {habit.Name}");

            _history.IsVisible = layout.HistoryDays > 0;
            _history.Show(item.History, today, layout.HistoryDays, layout.DotSize, layout.DotGap, done);
            var doneDays = 0;
            for (var i = item.History.Length - layout.HistoryDays; i < item.History.Length; i++)
            {
                doneDays += i >= 0 && item.History[i] == HabitDayState.Done ? 1 : 0;
            }
            AutomationProperties.SetName(_history, $"{habit.Name}, done on {doneDays} of the last {layout.HistoryDays} days");
        }
    }

    // Form -------------------------------------------------------------------------------------------------------------------

    private WidgetFrame? FindAncestorFrame() => Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<WidgetFrame>(this);

    private void LoadForm()
    {
        var h = _habits!;
        _name.Text = h.FormName;
        _emoji.Text = h.FormEmoji;
        foreach (var child in _weekdays.Children)
        {
            if (child is ToggleButton { Tag: DayOfWeek day } toggle)
            {
                toggle.IsChecked = h.FormDays.Includes(day);
            }
        }
        ChooseColor(h.FormColor);
        _form.Offset = default;
    }

    private void ChooseColor(CountdownColor color)
    {
        _formColor = color;
        foreach (var child in _swatches.Children)
        {
            if (child is Button { Tag: CountdownColor c, Content: Ellipse ellipse } swatch)
            {
                swatch.Classes.Set("selected", c == color);
                ellipse.Fill = new SolidColorBrush(ThemeColors.CountdownTag(c, Dark));
                ellipse.Stroke = c == CountdownColor.None ? ThemeService.Brush(this, "DimTextBrush") : null;
                ellipse.StrokeThickness = 1.2;
            }
        }
    }

    private void SaveForm()
    {
        if (_habits is not { } h)
        {
            return;
        }
        var days = HabitDays.None;
        foreach (var child in _weekdays.Children)
        {
            if (child is ToggleButton { Tag: DayOfWeek day, IsChecked: true })
            {
                days |= HabitDaysExtensions.Of(day);
            }
        }
        h.FormName = _name.Text ?? "";
        h.FormEmoji = _emoji.Text ?? "";
        h.FormColor = _formColor;
        h.FormDays = days;
        h.Save();
        Update();
    }

    private void Move(int delta)
    {
        if (_habits is { EditingId: { } id } h)
        {
            h.Move(id, delta);
        }
    }
}
