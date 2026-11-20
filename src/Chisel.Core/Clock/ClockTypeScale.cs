namespace Chisel.Core.Clock;

// One type scale for every tool in the clock widget, worked out from the room its slot has. The base sizes match the rest of the
// app (body 14, caption 12, title 16); everything grows and shrinks together with the smaller side of the slot, so the tools keep
// the same relative sizes as the clock beside them instead of each view carrying its own fixed font and button sizes.
public sealed record ClockTypeScale(double Factor, double Display, double Title, double Body, double Caption, double Pad, double Gap,
    double ButtonMinWidth, double ButtonPadX, double ButtonPadY, bool Compact)
{
    // The slot size (its smaller side, in pixels) at which the base sizes apply.
    public const double Reference = 260;
    public const double MinFactor = 0.8;
    public const double MaxFactor = 1.5;

    public static ClockTypeScale For(double width, double height)
    {
        // Height counts a little more than width: a slot is usually wider than it is tall, and the text has to fit its height.
        var factor = Math.Clamp(Math.Min(width, height * 1.25) / Reference, MinFactor, MaxFactor);
        double Size(double basis, double floor) => Math.Max(floor, Math.Round(basis * factor));
        var body = Size(14, 12);
        return new ClockTypeScale(
            factor,
            Display: Size(40, 28),
            Title: Size(16, 14),
            Body: body,
            Caption: Size(12, 10),
            Pad: Size(8, 6),
            Gap: Size(6, 4),
            ButtonMinWidth: body * 5,
            ButtonPadX: Size(10, 8),
            ButtonPadY: Size(4, 3),
            Compact: height < 150 || width < 170);
    }
}
