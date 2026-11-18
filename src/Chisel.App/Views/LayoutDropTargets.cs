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

    // Whether dropping into gap `target` would put a widget back where it already is: the gap right above or
    // right below its own slot. `origin` is null for a widget coming from the tray, which has no home.
    public static bool IsHome((int Column, int Index) target, (int Column, int Index)? origin) =>
        origin is { } o && target.Column == o.Column && (target.Index == o.Index || target.Index == o.Index + 1);

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

    // The new-column gap the pointer is in, while it is level with the columns; null otherwise. The outer
    // `edgeFraction` of the dashboard's width on each side counts for the two edge gaps, and a zone `dividerWidth`
    // wide centered on each divider for the gaps between columns. Only gaps in `allowed` are offered.
    public static int? FindNewColumn(Point pointer, IReadOnlyList<Rect> columns, double hostWidth, double gap,
        double edgeFraction, double dividerWidth, IReadOnlyCollection<int> allowed)
    {
        if (columns.Count == 0 || pointer.Y < columns.Min(c => c.Top) || pointer.Y > columns.Max(c => c.Bottom))
        {
            return null;
        }
        var edge = hostWidth * edgeFraction;
        if (pointer.X < edge)
        {
            return allowed.Contains(0) ? 0 : null;
        }
        if (pointer.X > hostWidth - edge)
        {
            return allowed.Contains(columns.Count) ? columns.Count : null;
        }
        var centers = NewColumnCenters(columns, gap);
        for (var i = 1; i < columns.Count; i++)
        {
            if (Math.Abs(pointer.X - centers[i]) <= dividerWidth / 2 && allowed.Contains(i))
            {
                return i;
            }
        }
        return null;
    }

    // Where a new column at gap `index` would land: an equal share of the width, centered on the gap and kept
    // inside the dashboard, as tall as the columns.
    public static Rect PhantomColumn(int index, IReadOnlyList<Rect> columns, double hostWidth, double gap)
    {
        var width = (hostWidth - gap * columns.Count) / (columns.Count + 1);
        var left = Math.Clamp(NewColumnCenters(columns, gap)[index] - width / 2, 0, Math.Max(0, hostWidth - width));
        var top = columns.Min(c => c.Top);
        return new Rect(left, top, width, columns.Max(c => c.Bottom) - top);
    }

    // The y of the insertion line for a gap in a column: above the first widget, between two, or below the last.
    public static double InsertionY(Rect column, IReadOnlyList<Rect> widgets, int index, double gap)
    {
        if (widgets.Count == 0)
        {
            return column.Top;
        }
        if (index <= 0)
        {
            return widgets[0].Top - gap / 2;
        }
        return index >= widgets.Count ? widgets[^1].Bottom + gap / 2 : (widgets[index - 1].Bottom + widgets[index].Top) / 2;
    }
}
