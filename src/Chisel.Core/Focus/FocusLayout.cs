namespace Chisel.Core.Focus;

// How much the focus timer widget shows for its size.
public enum FocusDetail
{
    Minimal, // the ring (when there is room for it) and the time left; clicking it starts and pauses
    Medium,  // and the phase name above the time and the session dots under it
    Full,    // and the Start / Skip / Reset buttons and, under them, today's count and the task in focus
}

// Every size in the widget, worked out from the room it has. All the thresholds are here, to tune in one place. The dial is the
// largest circle that fits above the rows under it; the text inside it is sized so the time, the label and the dots always fit,
// so nothing is ever clipped.
public sealed record FocusLayout(
    FocusDetail Detail, bool ShowRing, double Pad, double Diameter, double RingThickness, double TextRoom,
    double TimeFont, double LabelFont, double DotSize, double DotGap, double BelowHeight)
{
    public const double RingFrom = 56;        // below this diameter there is no ring, only the digits
    public const double MediumFrom = 120;     // from this diameter the phase name and the session dots
    public const double FullMinWidth = 180;   // the inner room the buttons and the rows under the dial need
    public const double FullMinHeight = 220;

    public const double ControlsHeight = 32;
    public const double FooterLineHeight = 18;
    public const int FooterLines = 2;         // "3 sessions today" and the task in focus
    public const double FooterFont = 12;
    public const double Gap = 8;

    public const double RingFraction = 0.07;  // the ring's thickness as a part of the diameter
    public const double MinRing = 3;
    public const double MaxRing = 14;
    public const double MaxRingTimeFont = 64;
    public const double MaxDigitalTimeFont = 96;
    public const double MinLabelFont = 8;
    public const double MaxLabelFont = 16;
    public const double MaxDotSize = 10;

    // The width of the time text in ems: "25:00", and "1:30:00" for phases of an hour or more. And a line's height in ems.
    public const double ShortTimeEms = 3.0;
    public const double LongTimeEms = 3.9;
    public const double LineHeight = 1.3;

    public bool ShowLabel => Detail >= FocusDetail.Medium;
    public bool ShowDots => Detail >= FocusDetail.Medium;
    public bool ShowControls => Detail == FocusDetail.Full;

    // longTime: the phase is an hour or longer (the time has hours). dots: how many session dots are drawn.
    public static FocusLayout For(double width, double height, FocusFace face, bool longTime, int dots)
    {
        width = Math.Max(width, 1);
        height = Math.Max(height, 1);
        dots = Math.Max(dots, 1);
        var pad = Math.Clamp(Math.Round(Math.Min(width, height) * 0.03), 2, 10);
        var innerW = Math.Max(0, width - 2 * pad);
        var innerH = Math.Max(0, height - 2 * pad);

        var full = innerW >= FullMinWidth && innerH >= FullMinHeight;
        var below = full ? Gap + ControlsHeight + Gap + FooterLines * FooterLineHeight : 0;
        var diameter = Math.Max(0, Math.Min(innerW, innerH - below));
        var detail = full ? FocusDetail.Full : diameter >= MediumFrom ? FocusDetail.Medium : FocusDetail.Minimal;
        var ring = face == FocusFace.Ring && diameter >= RingFrom;
        var thickness = ring ? Math.Clamp(diameter * RingFraction, MinRing, MaxRing) : 0;

        // Inside the ring the text keeps clear of it (a box that fits the circle); with no ring it has the whole width.
        var room = ring ? (diameter - 2 * thickness) * 0.8 : innerW;
        var tall = ring ? room : diameter;

        var medium = detail >= FocusDetail.Medium;
        var label = medium ? Math.Clamp(room * 0.1, MinLabelFont, MaxLabelFont) : 0;
        var dot = medium ? Math.Min(Math.Clamp(room * 0.06, 3, MaxDotSize), room / (1.8 * dots - 0.8)) : 0;
        var labelLine = label * LineHeight;
        var time = Math.Min(Math.Min(room / (longTime ? LongTimeEms : ShortTimeEms), (tall - labelLine - 2 * dot) / LineHeight),
            ring ? MaxRingTimeFont : MaxDigitalTimeFont);
        if (time < 0)
        {
            time = 0;
        }
        return new FocusLayout(detail, ring, pad, diameter, thickness, room, time, label, dot, dot * 0.8, below);
    }
}
