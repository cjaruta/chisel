namespace Chisel.Core.Clock;

// When the analog clock's numerals are drawn: only while the dial is large enough for them to be easy to read. Below this diameter
// (in pixels) the numerals are left off and the marks and hands carry the face.
public static class ClockDialLayout
{
    public const double NumeralsFromDiameter = 120;

    public static bool ShowsNumerals(double diameter) => diameter >= NumeralsFromDiameter;
}
