namespace Chisel.Core.Clock;

// Hand angles in degrees, clockwise from 12.
public readonly record struct HandAngles(double Hour, double Minute, double Second);

public static class ClockMath
{
    // The hour hand includes the minute fraction and the minute hand the second fraction, so they move smoothly.
    // The second hand steps whole seconds.
    public static HandAngles ClockAngles(TimeOnly time)
    {
        var minute = time.Minute + time.Second / 60.0;
        var hour = time.Hour % 12 + minute / 60.0;
        return new HandAngles(hour * 30, minute * 6, time.Second * 6);
    }
}
