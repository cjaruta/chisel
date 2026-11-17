using System;
using System.Collections.Generic;
using System.Linq;
using Chisel.Core.Schedule;

namespace Chisel.App.ViewModels;

// One drawable piece of a block on the day grid, in minutes from midnight (0 to 1440).
// An overnight block gives two pieces. Overlapping pieces share the width: Column of ColumnCount.
public sealed record GridSegment(int BlockIndex, int StartMinute, int EndMinute, int Column, int ColumnCount);

public static class ScheduleGridLayout
{
    public const int MinutesPerDay = 24 * 60;

    public static double MinuteOfDay(TimeOnly time) => time.ToTimeSpan().TotalMinutes;

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
