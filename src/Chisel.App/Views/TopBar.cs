namespace Chisel.App.Views;

public enum ToolbarPlacement
{
    // Centered on the window, clear of the clock and the buttons.
    Centered,
    // Centered in the space between the clock and the buttons (centering on the window would cover one of them).
    BetweenClockAndButtons,
    // Its own line below the top bar, when it fits nowhere beside them.
    OwnLine,
}

// How the top bar fits the window width: the clock shrinks in two steps on narrower windows, and the edit toolbar goes
// where its whole row fits without covering the clock or the buttons.
public static class TopBar
{
    public const double Medium = 900;
    public const double Narrow = 600;

    public static (double Time, double Date) ClockFontSizes(double width) =>
        width < Narrow ? (26, 15) : width < Medium ? (30, 17) : (36, 20);

    // left / right: the room the clock and the buttons take at each side, gaps included.
    public static ToolbarPlacement PlaceToolbar(double width, double toolbar, double left, double right)
    {
        var start = (width - toolbar) / 2;
        if (toolbar > 0 && start >= left && start + toolbar <= width - right)
        {
            return ToolbarPlacement.Centered;
        }
        return toolbar > 0 && toolbar <= width - left - right ? ToolbarPlacement.BetweenClockAndButtons : ToolbarPlacement.OwnLine;
    }
}
