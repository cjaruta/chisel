namespace Chisel.Core.Clock;

// How much the analog stopwatch face shows, and how big each part is, for the diameter it is drawn at. Everything depends on the
// diameter alone, so the face works the numbers out once when its size changes. All the thresholds are here, to tune in one place.
public enum StopwatchDetail
{
    Minimal, // the rim, the major ticks and the seconds hand
    Small,   // and the digital readout beneath
    Medium,  // and the minor ticks and numbers (the four main ones, then all twelve as the dial grows)
    Large,   // and the minutes sub-dial
}

public enum StopwatchNumbers { None, Main, All }

public sealed record StopwatchMetrics(
    StopwatchDetail Detail, double Diameter, double Scale,
    bool ShowReadout, bool MinorTicks, StopwatchNumbers Numbers, bool SubDial,
    double TickInset, double MajorTickLength, double MinorTickLength, double MajorTickWidth, double MinorTickWidth,
    double NumberFont, double NumberRadius,
    double SubOffset, double SubRadius, double SubFont,
    double SecondLength, double SecondTail, double MinuteLength, double MinuteTail)
{
    private double Radius => Diameter / 2;

    // The distance from the center where the ticks start (the inner end of a major tick) and where the numbers end.
    public double TickInnerEdge => Radius - TickInset - MajorTickLength;

    public double NumberOuterEdge => NumberRadius + NumberFont * 0.75;

    // A stroke that follows the diameter and is never thinner than a pixel; a style with none (0) stays without.
    public double Stroke(double styleWidth) => StopwatchLayout.Stroke(styleWidth, Diameter);
}

public static class StopwatchLayout
{
    // The smallest diameter at which each level starts. Numerals follow their own two thresholds inside these: the numbers stay
    // until the dial is too small to show them legibly (NumbersFrom), thinning from twelve to the four main ones on the way down.
    public const double SmallFrom = 72;
    public const double NumbersFrom = 96;      // below this no numerals at all (the smallest size that keeps them readable)
    public const double AllNumbersFrom = 140;  // from here all twelve; between the two only the four at 12, 3, 6 and 9
    public const double LargeFrom = 180;       // from here the minutes sub-dial too
    public const double MediumFrom = NumbersFrom;

    // The smallest and largest numeral size; between them it is a fraction of the diameter.
    public const double MinNumberFont = 7;
    public const double MaxNumberFont = 20;
    public const double NumberFontFraction = 0.055;

    // The diameter at which the style's own widths are used as they are; others scale with the diameter.
    public const double ReferenceDiameter = 160;
    public const double MaxStrokeScale = 1.5;

    // The room kept between the face and the edge of the space it is given, and the smallest diameter drawn at all.
    public const double EdgeGap = 6;
    public const double MinDiameter = 16;

    public const double NumberRadiusFactor = 0.70;

    public static double DiameterFor(double room) => Math.Max(0, room - 2 * EdgeGap);

    public static StopwatchDetail DetailFor(double diameter) =>
        diameter >= LargeFrom ? StopwatchDetail.Large : diameter >= MediumFrom ? StopwatchDetail.Medium : diameter >= SmallFrom ? StopwatchDetail.Small : StopwatchDetail.Minimal;

    public static double Stroke(double styleWidth, double diameter) =>
        styleWidth <= 0 ? 0 : Math.Max(1, styleWidth * Math.Clamp(diameter / ReferenceDiameter, 0, MaxStrokeScale));

    public static StopwatchMetrics For(double diameter)
    {
        diameter = Math.Max(0, diameter);
        var detail = DetailFor(diameter);
        var r = diameter / 2;
        var scale = Math.Clamp(diameter / ReferenceDiameter, 0, MaxStrokeScale);
        var sub = r * 0.22;
        return new StopwatchMetrics(
            detail, diameter, scale,
            ShowReadout: detail >= StopwatchDetail.Small,
            MinorTicks: detail >= StopwatchDetail.Medium,
            Numbers: diameter >= AllNumbersFrom ? StopwatchNumbers.All : diameter >= NumbersFrom ? StopwatchNumbers.Main : StopwatchNumbers.None,
            SubDial: detail == StopwatchDetail.Large,
            TickInset: Math.Max(2, r * 0.04), MajorTickLength: r * 0.1, MinorTickLength: r * 0.04,
            MajorTickWidth: Math.Max(1.25, diameter * 0.016), MinorTickWidth: Math.Max(1, diameter * 0.006),
            NumberFont: Math.Clamp(diameter * NumberFontFraction, MinNumberFont, MaxNumberFont), NumberRadius: r * NumberRadiusFactor,
            SubOffset: r * 0.34, SubRadius: sub, SubFont: Math.Clamp(sub * 0.34, 6, 14),
            SecondLength: r * 0.86, SecondTail: r * 0.18, MinuteLength: sub * 0.85, MinuteTail: sub * 0.15);
    }
}
