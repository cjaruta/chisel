using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Chisel.App.Theming;
using Chisel.Core.Countdown;
using Chisel.Core.Habits;

namespace Chisel.App.Views.Widgets;

// The habit summary ring: a track and the done part in the accent. When everything due today is done it turns green, fills, and
// shows a quiet check mark in place of the count.
public sealed class HabitRing : Control
{
    private double _fraction;
    private bool _allDone;

    public double Fraction
    {
        get => _fraction;
        set
        {
            if (_fraction != value)
            {
                _fraction = value;
                InvalidateVisual();
            }
        }
    }

    public bool AllDone
    {
        get => _allDone;
        set
        {
            if (_allDone != value)
            {
                _allDone = value;
                InvalidateVisual();
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 2)
        {
            return;
        }
        var thickness = Math.Clamp(size * 0.09, 2, 12);
        var radius = (size - thickness) / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var green = ThemeColors.CountdownTag(CountdownColor.Green, ActualThemeVariant == ThemeVariant.Dark);
        var color = AllDone ? new ImmutableSolidColorBrush(green) : ThemeService.Brush(this, "AccentBrush");

        context.DrawEllipse(null, new Pen(ThemeService.Brush(this, "GridLineBrush"), thickness), center, radius, radius);
        if (AllDone)
        {
            context.DrawEllipse(new ImmutableSolidColorBrush(green.WithAlpha(0x22)), new Pen(color, thickness), center, radius, radius);
            var s = radius * 0.5;
            var check = new StreamGeometry();
            using (var g = check.Open())
            {
                g.BeginFigure(new Point(center.X - s * 0.8, center.Y + s * 0.05), false);
                g.LineTo(new Point(center.X - s * 0.2, center.Y + s * 0.6));
                g.LineTo(new Point(center.X + s * 0.85, center.Y - s * 0.55));
                g.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(color, Math.Max(1.5, thickness * 0.9), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), check);
            return;
        }
        var fraction = Math.Clamp(Fraction, 0, 1);
        if (fraction <= 0)
        {
            return;
        }
        var arc = new StreamGeometry();
        using (var g = arc.Open())
        {
            var start = new Point(center.X, center.Y - radius);
            var angle = fraction * 2 * Math.PI;
            var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
            g.BeginFigure(start, false);
            if (fraction >= 1)
            {
                g.ArcTo(new Point(center.X, center.Y + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                g.ArcTo(start, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            }
            else
            {
                g.ArcTo(end, new Size(radius, radius), 0, fraction > 0.5, SweepDirection.Clockwise);
            }
            g.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(color, thickness, lineCap: PenLineCap.Round), arc);
    }
}

// A habit's recent days drawn in one pass, in rows of seven with today last (bottom right): the last week as dots, or the last
// five weeks as a small grid. Done days are filled in the habit's color, missed days are an outline, days off are a faint speck
// and today, while due and unchecked, has a colored outline. Clicking a day (never one after today) asks for it to be toggled.
public sealed class HabitHistory : Control
{
    private HabitDayState[] _states = [];
    private DateOnly _today;
    private IBrush? _fill;

    public HabitHistory()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    // How many days are drawn: 7 (dots) or 35 (the grid).
    public int Days { get; private set; }

    public double Dot { get; private set; }

    public double Gap { get; private set; }

    public Action<DateOnly>? DayClicked { get; set; }

    // states: oldest first, today last; only the last `days` are drawn.
    public void Show(HabitDayState[] states, DateOnly today, int days, double dot, double gap, IBrush fill)
    {
        _states = states;
        _today = today;
        _fill = fill;
        Days = Math.Min(days, states.Length);
        Dot = dot;
        Gap = gap;
        var rows = (Days + HabitLayout.WeekDays - 1) / HabitLayout.WeekDays;
        Width = Days == 0 ? 0 : HabitLayout.WeekDays * dot + (HabitLayout.WeekDays - 1) * gap;
        Height = rows == 0 ? 0 : rows * dot + (rows - 1) * gap;
        InvalidateVisual();
    }

    private DateOnly DateOf(int index) => _today.AddDays(index - (Days - 1));

    public Point CellCenter(DateOnly date)
    {
        var index = date.DayNumber - _today.DayNumber + Days - 1;
        var (row, col) = (index / HabitLayout.WeekDays, index % HabitLayout.WeekDays);
        return new Point(col * (Dot + Gap) + Dot / 2, row * (Dot + Gap) + Dot / 2);
    }

    // The day under a point, or null between or outside the cells.
    public DateOnly? DayAt(Point p)
    {
        var step = Dot + Gap;
        if (Days == 0 || step <= 0 || p.X < 0 || p.Y < 0)
        {
            return null;
        }
        var col = (int)(p.X / step);
        var row = (int)(p.Y / step);
        var index = row * HabitLayout.WeekDays + col;
        return col < HabitLayout.WeekDays && index < Days ? DateOf(index) : null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && DayAt(e.GetPosition(this)) is { } day && day <= _today)
        {
            DayClicked?.Invoke(day);
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Days == 0 || Dot <= 0)
        {
            return;
        }
        var grid = Days > HabitLayout.WeekDays;
        var radius = grid ? Dot * 0.25 : Dot / 2;
        var dim = ThemeService.Brush(this, "DimTextBrush");
        var line = new Pen(ThemeService.Brush(this, "GridLineBrush"), Math.Max(1, Dot * 0.12));
        var pending = new Pen(_fill, Math.Max(1.2, Dot * 0.16));
        var offset = _states.Length - Days;
        for (var i = 0; i < Days; i++)
        {
            var (row, col) = (i / HabitLayout.WeekDays, i % HabitLayout.WeekDays);
            var cell = new Rect(col * (Dot + Gap), row * (Dot + Gap), Dot, Dot);
            switch (_states[offset + i])
            {
                case HabitDayState.Done:
                    context.DrawRectangle(_fill, null, cell, radius, radius);
                    break;
                case HabitDayState.Missed:
                    context.DrawRectangle(null, line, cell.Deflate(line.Thickness / 2), radius, radius);
                    break;
                case HabitDayState.Pending:
                    context.DrawRectangle(null, pending, cell.Deflate(pending.Thickness / 2), radius, radius);
                    break;
                default:
                    var speck = Math.Max(1, Dot * 0.18);
                    context.DrawEllipse(dim, null, cell.Center, speck, speck);
                    break;
            }
        }
    }
}
