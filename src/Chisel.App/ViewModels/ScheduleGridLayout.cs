using System;
using System.Collections.Generic;
using System.Linq;
using Chisel.Core.Schedule;

namespace Chisel.App.ViewModels;

// One drawable piece of a block on the day grid, in minutes from midnight (0 to 1440).
// An overnight block gives two pieces. Overlapping pieces share the width: Column of ColumnCount.
public sealed record GridSegment(int BlockIndex, int StartMinute, int EndMinute, int Column, int ColumnCount);

// A time range on the grid in minutes from midnight. EndMinute 1440 is midnight at the end of the day,
// which a block stores as an end of 00:00.
public sealed record GridRange(int StartMinute, int EndMinute)
{
    public TimeOnly Start => TimeAt(StartMinute);
    public TimeOnly End => TimeAt(EndMinute);

    public static TimeOnly TimeAt(int minute) => TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute % ScheduleGridLayout.MinutesPerDay));
}

public static class ScheduleGridLayout
{
    public const int MinutesPerDay = 24 * 60;

    // The hover guide line and click-to-add snap to this many minutes.
    public const int HoverSnapMinutes = 15;

    public static double MinuteOfDay(TimeOnly time) => time.ToTimeSpan().TotalMinutes;

    // y: pointer position in the viewport, measured from where midnight would be with no scrolling.
    // scrollOffset: how far the grid is scrolled down. Rounds to the nearest mark, kept within the day.
    public static TimeOnly SnapTime(double y, double scrollOffset, double pixelsPerMinute = 1.0) =>
        GridRange.TimeAt(SnapMinute(y, scrollOffset, pixelsPerMinute, MinutesPerDay - HoverSnapMinutes));

    // A dragged range: the press and current positions (same meaning as in SnapTime), snapped,
    // earlier one first, at least one interval long, and inside the day. The end may be 1440 (midnight).
    public static GridRange DragRange(double pressY, double currentY, double scrollOffset, double pixelsPerMinute = 1.0)
    {
        var press = SnapMinute(pressY, scrollOffset, pixelsPerMinute, MinutesPerDay - HoverSnapMinutes);
        var current = SnapMinute(currentY, scrollOffset, pixelsPerMinute, MinutesPerDay);
        if (current == press)
        {
            return new GridRange(press, press + HoverSnapMinutes);
        }
        return new GridRange(Math.Min(press, current), Math.Max(press, current));
    }

    // The nearest mark to a position, as minutes from midnight between 0 and max.
    public static int SnapMinute(double y, double scrollOffset, double pixelsPerMinute = 1.0, int max = MinutesPerDay)
    {
        var minute = (y + scrollOffset) / pixelsPerMinute;
        var marks = Math.Round(minute / HoverSnapMinutes, MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(marks * HoverSnapMinutes, 0, max);
    }

    public static IReadOnlyList<GridSegment> Segments(IReadOnlyList<ScheduleBlock> blocks)
    {
        var pieces = new List<(int Index, int Start, int End)>();
        for (var i = 0; i < blocks.Count; i++)
        {
            var start = (int)MinuteOfDay(blocks[i].Start);
            var end = (int)MinuteOfDay(blocks[i].End);
            if (!blocks[i].CrossesMidnight)
            {
                pieces.Add((i, start, end));
                continue;
            }

            if (end > 0)
            {
                pieces.Add((i, 0, end));
            }
            pieces.Add((i, start, MinutesPerDay));
        }

        // Longer pieces first on a tie, so the bigger block takes the left column.
        var ordered = pieces.OrderBy(p => p.Start).ThenByDescending(p => p.End).ThenBy(p => p.Index).ToList();

        var result = new List<GridSegment>();
        var group = new List<(int Index, int Start, int End, int Column)>();
        var columnEnds = new List<int>();
        var groupEnd = int.MinValue;

        void CloseGroup()
        {
            result.AddRange(group.Select(p => new GridSegment(p.Index, p.Start, p.End, p.Column, columnEnds.Count)));
            group.Clear();
            columnEnds.Clear();
        }

        foreach (var piece in ordered)
        {
            if (piece.Start >= groupEnd)
            {
                CloseGroup();
            }

            var column = columnEnds.FindIndex(end => end <= piece.Start);
            if (column < 0)
            {
                column = columnEnds.Count;
                columnEnds.Add(piece.End);
            }
            else
            {
                columnEnds[column] = piece.End;
            }

            group.Add((piece.Index, piece.Start, piece.End, column));
            groupEnd = group.Count == 1 ? piece.End : Math.Max(groupEnd, piece.End);
        }
        CloseGroup();

        return result;
    }
}
