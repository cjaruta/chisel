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
}
