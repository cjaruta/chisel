namespace Chisel.Core.Clock;

public readonly record struct ClockLabel(int Hour, string Text);

public static class ClockLabels
{
    private static readonly string[] Roman = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];

    public static IReadOnlyList<ClockLabel> For(NumeralStyle style) => style switch
    {
        NumeralStyle.Twelve => [new(12, "12")],
        NumeralStyle.Four => [new(12, "12"), new(3, "3"), new(6, "6"), new(9, "9")],
        NumeralStyle.All => Enumerable.Range(1, 12).Select(h => new ClockLabel(h, h.ToString())).ToList(),
        NumeralStyle.Roman => Enumerable.Range(1, 12).Select(h => new ClockLabel(h, Roman[h - 1])).ToList(),
        _ => [],
    };

    // The labels of a 24-hour dial: every three hours, midnight ("24") at the top.
    public static IReadOnlyList<ClockLabel> TwentyFour() =>
        new ClockLabel[] { new(0, "24"), new(3, "3"), new(6, "6"), new(9, "9"), new(12, "12"), new(15, "15"), new(18, "18"), new(21, "21") };

    // Degrees clockwise from 12 for an hour label.
    public static double AngleOf(int hour) => hour % 12 * 30;

    // The same on a 24-hour dial.
    public static double AngleOf24(int hour) => hour % 24 * 15;
}
