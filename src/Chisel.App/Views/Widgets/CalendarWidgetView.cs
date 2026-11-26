using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.Core.Calendar;

namespace Chisel.App.Views.Widgets;

// The calendar widget: a month title with previous/next/Today buttons, the weekday names, and six weeks of days (so the grid
// never jumps between months), with an optional column of week numbers. What is shown and every size come from CalendarLayout,
// worked out from the widget's actual size. The controls are built once and updated in place. Day buttons do not take focus; the
// widget does, and handles the keyboard (arrows move by a day or a week, PageUp/PageDown by a month, Home goes to today, M marks
// the selected day). Right-click marks a day too. In edit mode the frame makes the body inert so the card drags like the others.
// A card too short for readable dates scrolls this view (see CalendarLayout.MinHeight).
public sealed class CalendarWidgetView : WidgetView
{
    private readonly Grid _root = new() { Name = "CalendarRoot", RowDefinitions = new RowDefinitions("Auto,Auto,*") };
    private readonly Grid _header = new() { Name = "CalendarHeader", ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };
    private readonly TextBlock _titleText = new() { Name = "CalendarTitleText", FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _title;
    private readonly Button _previous;
    private readonly Button _next;
    private readonly Button _today;
    private readonly Grid _weekdays = new() { Name = "CalendarWeekdays" };
    private readonly TextBlock[] _weekdayText = new TextBlock[MonthGrid.Columns];
    private readonly Grid _grid = new() { Name = "CalendarGrid" };
    private readonly TextBlock[] _weekText = new TextBlock[MonthGrid.Rows];
    private readonly List<(Button Button, TextBlock Text, Ellipse Dot)> _cells = [];
    private CalendarViewModel? _calendar;

    // The layout for the widget's current size.
    public CalendarLayout Layout { get; private set; } = CalendarLayout.For(0, 0, false);

    public CalendarWidgetView()
    {
        Focusable = true;
        ClipToBounds = true;
        Margin = new Thickness(0);
        VerticalAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "Calendar");

        _title = NavButton("CalendarTitle", _titleText, "Go to the current month", "calTitle", () => _calendar?.GoToTodayCommand.Execute(null));
        _title.HorizontalAlignment = HorizontalAlignment.Left;
        _previous = NavButton("CalendarPrevious", Chevron("M10,3 L5,8 L10,13"), "Previous month", "calNav", () => _calendar?.PreviousMonthCommand.Execute(null));
        _next = NavButton("CalendarNext", Chevron("M6,3 L11,8 L6,13"), "Next month", "calNav", () => _calendar?.NextMonthCommand.Execute(null));
        _today = NavButton("CalendarToday", new TextBlock { Text = "Today", VerticalAlignment = VerticalAlignment.Center }, "Today", "calNav", () => _calendar?.GoToTodayCommand.Execute(null));
        _header.Children.Add(_title);
        Grid.SetColumn(_today, 1);
        Grid.SetColumn(_previous, 2);
        Grid.SetColumn(_next, 3);
        _header.Children.Add(_today);
        _header.Children.Add(_previous);
        _header.Children.Add(_next);

        _weekdays.ColumnDefinitions = Columns();
        for (var c = 0; c < MonthGrid.Columns; c++)
        {
            var text = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };
            text.Classes.Add("calWeekday");
            Grid.SetColumn(text, c + 1);
            _weekdays.Children.Add(text);
            _weekdayText[c] = text;
        }

        _grid.ColumnDefinitions = Columns();
        for (var r = 0; r < MonthGrid.Rows; r++)
        {
            _grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            var week = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            week.Classes.Add("calWeekNumber");
            Grid.SetRow(week, r);
            _grid.Children.Add(week);
            _weekText[r] = week;
            for (var c = 0; c < MonthGrid.Columns; c++)
            {
                AddCell(r, c);
            }
        }

        _root.Children.Add(_header);
        Grid.SetRow(_weekdays, 1);
        _root.Children.Add(_weekdays);
        Grid.SetRow(_grid, 2);
        _root.Children.Add(_grid);
        Content = _root;

        AddHandler(PointerPressedEvent, (_, _) => Focus(), handledEventsToo: true);
    }

    // A week-number column (0 wide when hidden), then the seven days.
    private static ColumnDefinitions Columns()
    {
        var columns = new ColumnDefinitions { new ColumnDefinition(0, GridUnitType.Pixel) };
        for (var c = 0; c < MonthGrid.Columns; c++)
        {
            columns.Add(new ColumnDefinition(1, GridUnitType.Star));
        }
        return columns;
    }

    private static Control Chevron(string data) => new Path
    {
        Data = Geometry.Parse(data), Width = 16, Height = 16, Stretch = Stretch.Uniform, StrokeThickness = 1.8,
        StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, IsHitTestVisible = false,
    };

    private static Button NavButton(string name, Control content, string label, string style, Action click)
    {
        var button = new Button { Name = name, Content = content, Focusable = false, VerticalAlignment = VerticalAlignment.Center };
        button.Classes.Add(style);
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        button.Click += (_, _) => click();
        return button;
    }

    private void AddCell(int row, int column)
    {
        var text = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        var dot = new Ellipse { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false, IsVisible = false };
        dot.Classes.Add("calDot");
        var content = new Grid { IsHitTestVisible = false };
        content.Children.Add(text);
        content.Children.Add(dot);
        var button = new Button
        {
            Name = "CalendarDay", Content = content, Focusable = false,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add("calDay");
        button.Click += (_, _) =>
        {
            if (button.Tag is DateOnly date)
            {
                _calendar?.Select(date);
            }
        };
        button.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Right && button.Tag is DateOnly date && _calendar is { } calendar)
            {
                calendar.Select(date);
                calendar.ToggleMarkCommand.Execute(null);
                e.Handled = true;
            }
        }, handledEventsToo: true);
        Grid.SetRow(button, row);
        Grid.SetColumn(button, column + 1);
        _grid.Children.Add(button);
        _cells.Add((button, text, dot));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _calendar = (DataContext as MainViewModel)?.Calendar;
        if (_calendar is null)
        {
            return;
        }
        _calendar.PropertyChanged += OnCalendarChanged;
        Relayout();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_calendar is not null)
        {
            _calendar.PropertyChanged -= OnCalendarChanged;
            _calendar = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Relayout();
    }

    private void OnCalendarChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalendarViewModel.ShowWeekNumbers))
        {
            Relayout();
        }
        else if (e.PropertyName == nameof(CalendarViewModel.Days))
        {
            Update();
        }
    }

    private void Relayout()
    {
        if (_calendar is null)
        {
            return;
        }
        Layout = CalendarLayout.For(Bounds.Width, Bounds.Height, _calendar.ShowWeekNumbers);
        var layout = Layout;
        MinHeight = layout.MinHeight;
        _root.Margin = new Thickness(layout.Pad);
        _root.RowDefinitions[0].Height = new GridLength(layout.HeaderHeight);
        _root.RowDefinitions[1].Height = new GridLength(layout.WeekdayHeight);
        _weekdays.IsVisible = layout.ShowWeekdayNames;
        _previous.IsVisible = layout.ShowNavigation;
        _next.IsVisible = layout.ShowNavigation;
        _today.IsVisible = layout.ShowTodayButton;
        _weekdays.ColumnDefinitions[0].Width = new GridLength(layout.WeekNumberWidth);
        _grid.ColumnDefinitions[0].Width = new GridLength(layout.WeekNumberWidth);

        _titleText.FontSize = layout.TitleFont;
        foreach (var button in new[] { _previous, _next, _today })
        {
            button.Height = layout.ButtonSize;
            button.MinWidth = layout.ButtonSize;
            button.CornerRadius = new CornerRadius(layout.ButtonSize / 2);
            button.FontSize = layout.SmallFont + 1;
        }
        _title.Height = Math.Max(layout.ButtonSize, layout.TitleFont * 1.5);
        foreach (var text in _weekdayText)
        {
            text.FontSize = layout.SmallFont;
        }
        foreach (var text in _weekText)
        {
            text.FontSize = layout.SmallFont;
            text.IsVisible = layout.ShowWeekNumbers;
        }

        // Each day is a circle as large as its cell allows, with a little air around it.
        var size = Math.Max(4, Math.Min(layout.CellWidth, layout.CellHeight) - Math.Max(2, layout.CellHeight * 0.08));
        var dot = Math.Max(3, Math.Round(layout.DayFont * 0.22));
        foreach (var (button, text, marker) in _cells)
        {
            button.Width = size;
            button.Height = size;
            button.CornerRadius = new CornerRadius(size / 2);
            text.FontSize = layout.DayFont;
            marker.Width = dot;
            marker.Height = dot;
            marker.Margin = new Thickness(0, 0, 0, Math.Max(1, (size - layout.DayFont * 1.2) / 2 - dot - 1));
        }
        Update();
    }

    // Puts the view model's month into the controls.
    private void Update()
    {
        if (_calendar is not { } calendar)
        {
            return;
        }
        var layout = Layout;
        _titleText.Text = layout.ShowYear ? calendar.Title : calendar.MonthName;
        AutomationProperties.SetName(_title, $"{calendar.Title}. Go to the current month");

        var names = calendar.WeekdayNames;
        var fullNames = calendar.WeekdayFullNames;
        for (var c = 0; c < MonthGrid.Columns; c++)
        {
            _weekdayText[c].Text = names[c];
            AutomationProperties.SetName(_weekdayText[c], fullNames[c]);
        }
        var weeks = calendar.WeekNumbers;
        for (var r = 0; r < MonthGrid.Rows; r++)
        {
            _weekText[r].Text = weeks[r].ToString(System.Globalization.CultureInfo.CurrentCulture);
            AutomationProperties.SetName(_weekText[r], $"Week {weeks[r]}");
        }

        var days = calendar.Days;
        for (var i = 0; i < _cells.Count; i++)
        {
            var (button, text, dot) = _cells[i];
            var day = days[i];
            button.Tag = day.Date;
            text.Text = day.Label;
            dot.IsVisible = day.IsMarked;
            AutomationProperties.SetName(button, day.AutomationName);
            button.Classes.Set("muted", !day.InMonth);
            button.Classes.Set("today", day.IsToday);
            button.Classes.Set("selected", day.IsSelected);
            button.Classes.Set("marked", day.IsMarked);
        }
    }

    private bool InEditMode => this.FindAncestorOfType<WidgetFrame>() is { IsEditing: true };

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_calendar is not { } calendar || e.Handled || InEditMode || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        Action? action = e.Key switch
        {
            Key.Left => () => calendar.MoveSelection(-1),
            Key.Right => () => calendar.MoveSelection(1),
            Key.Up => () => calendar.MoveSelection(-7),
            Key.Down => () => calendar.MoveSelection(7),
            Key.PageUp => () => calendar.MoveMonths(-1),
            Key.PageDown => () => calendar.MoveMonths(1),
            Key.Home => () => calendar.GoToTodayCommand.Execute(null),
            Key.M => () => calendar.ToggleMarkCommand.Execute(null),
            _ => null,
        };
        if (action is not null)
        {
            action();
            e.Handled = true;
        }
    }
}
