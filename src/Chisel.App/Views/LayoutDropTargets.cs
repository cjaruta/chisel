using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace Chisel.App.Views;

// Where a dragged widget would land for a pointer position: a column and a gap in it
// (0 = above the first widget, Count = below the last). All rectangles are in the same coordinates.
public static class LayoutDropTargets
{
    // columns: each column's area. widgets: each column's widget cards, top to bottom.
    // gap: the space between columns; a pointer in it belongs to the nearer column.
    // Returns null when the pointer is not over any column.
    public static (int Column, int Index)? Find(Point pointer, IReadOnlyList<Rect> columns, IReadOnlyList<IReadOnlyList<Rect>> widgets, double gap)
    {
        for (var c = 0; c < columns.Count; c++)
        {
            var area = columns[c];
            var inX = pointer.X >= area.Left - gap / 2 && pointer.X < area.Right + gap / 2;
            var inY = pointer.Y >= area.Top && pointer.Y <= area.Bottom;
            if (inX && inY)
            {
                // The gap above the first widget whose middle is still below the pointer.
                return (c, widgets[c].Count(w => w.Center.Y < pointer.Y));
            }
        }
        return null;
    }

    // The x of each gap a new column could go into: left of the first column, between each pair, right of the
    // last (index 0 to Count). The edge gaps sit half a gap outside the columns.
    public static double[] NewColumnCenters(IReadOnlyList<Rect> columns, double gap)
    {
        var centers = new double[columns.Count + 1];
        if (columns.Count == 0)
        {
            return centers;
        }
        centers[0] = columns[0].Left - gap / 2;
        for (var i = 1; i < columns.Count; i++)
        {
            centers[i] = (columns[i - 1].Right + columns[i].Left) / 2;
        }
        centers[^1] = columns[^1].Right + gap / 2;
        return centers;
    }

    // The new-column gap the pointer is within `reach` of (horizontally), while it is level with the columns;
    // null otherwise.
    public static int? FindNewColumn(Point pointer, IReadOnlyList<Rect> columns, double gap, double reach)
    {
        if (columns.Count == 0 || pointer.Y < columns.Min(c => c.Top) || pointer.Y > columns.Max(c => c.Bottom))
        {
            return null;
        }
        var centers = NewColumnCenters(columns, gap);
        for (var i = 0; i < centers.Length; i++)
        {
            if (Math.Abs(pointer.X - centers[i]) <= reach)
            {
                return i;
            }
        }
        return null;
    }
}
