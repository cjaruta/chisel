using Avalonia;

namespace Chisel.App.Views;

// Scripted drag for the dev-only capture mode (--capture). Runs the same view code the pointer handlers run.
public sealed partial class LayoutHost
{
    internal void ScriptPickUp(string widgetId)
    {
        var frame = _frames[widgetId];
        _pressed = frame;
        _grabOffset = new Point(frame.Bounds.Width / 2, 12);
        _pressPoint = frame.TranslatePoint(_grabOffset, this) ?? default;
        Layout!.BeginWidgetDrag(widgetId);
        StartDragVisuals(frame, frame.Widget.Title);
    }

    internal void ScriptMove(Point point) => UpdateDrag(point);

    internal void ScriptDrop(Point point)
    {
        UpdateDrag(point);
        Drop(Layout!);
        EndDrag();
    }

    internal string DebugBorder(string widgetId) => $"{_frames[widgetId].BorderBrush} run={DropBorderRun?.Status}";

    internal Point CenterOf(string widgetId, double fraction)
    {
        var frame = _frames[widgetId];
        return frame.TranslatePoint(new Point(frame.Bounds.Width / 2, frame.Bounds.Height * fraction), this) ?? default;
    }
}
