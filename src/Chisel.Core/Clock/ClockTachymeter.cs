namespace Chisel.Core.Clock;

public readonly record struct TachymeterMark(int Speed, double Angle);

// The speed scale printed around a chronograph: time a distance of one unit with the seconds hand and read the speed in units per
// hour where it stops. A mark for speed v sits at the angle the seconds hand has reached after 3600 / v seconds, so 60 is at the top
// (one second), 120 at the bottom (half a minute).
public static class ClockTachymeter
{
    private static readonly int[] Speeds = [60, 65, 70, 75, 80, 90, 100, 120, 150, 200, 300, 500];

    public static IReadOnlyList<TachymeterMark> Marks { get; } = Speeds.Select(v => new TachymeterMark(v, 21600.0 / v % 360)).ToList();
}
