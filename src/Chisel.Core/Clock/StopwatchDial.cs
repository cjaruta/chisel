namespace Chisel.Core.Clock;

// Where the hands of the analog stopwatch point (degrees clockwise from the top) for an elapsed time in seconds. Both sweep: the
// seconds hand goes once round a minute, and the sub-dial's minutes hand once round half an hour, moving smoothly between its marks.
public readonly record struct StopwatchAngles(double Seconds, double Minutes);

public static class StopwatchDial
{
    // Minutes the sub-dial counts in one turn.
    public const double SubDialMinutes = 30;

    public static StopwatchAngles Angles(double elapsedSeconds)
    {
        var elapsed = Math.Max(0, elapsedSeconds);
        return new StopwatchAngles(elapsed % 60 * 6, elapsed / 60 % SubDialMinutes * (360 / SubDialMinutes));
    }
}
