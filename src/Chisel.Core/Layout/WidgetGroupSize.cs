namespace Chisel.Core.Layout;

public static class WidgetGroupSize
{
    // The least size a group's card can have: the largest minimum width and the largest minimum height of its
    // members, so each widget still fits when its tab is active. (0, 0) for no members.
    public static (double Width, double Height) Minimum(IEnumerable<(double Width, double Height)> members) =>
        members.Aggregate((Width: 0.0, Height: 0.0), (a, m) => (Math.Max(a.Width, m.Width), Math.Max(a.Height, m.Height)));
}
