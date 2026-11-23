using Chisel.Core.Clock;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Mapping;

public static class ClockStyleMapper
{
    public static ClockStyleEntity ToEntity(this ClockStyle style) => new()
    {
        Alignment = (int)style.Alignment, Shape = (int)style.Shape, BorderWidth = style.BorderWidth, Markers = (int)style.Markers,
        EmphasizeQuarters = style.EmphasizeQuarters, Numerals = (int)style.Numerals, Hands = (int)style.Hands,
        HourThickness = style.HourThickness, MinuteThickness = style.MinuteThickness, SecondThickness = style.SecondThickness,
        ShowSeconds = style.ShowSeconds, DialColor = style.Colors.Dial, MarkerColor = style.Colors.Markers,
        NumeralColor = style.Colors.Numerals, HourColor = style.Colors.Hour, MinuteColor = style.Colors.Minute, SecondColor = style.Colors.Second,
        Kind = (int)style.Kind, DialFill = style.DialFill, MinuteTrack = style.MinuteTrack, Tachymeter = style.Tachymeter, Stopwatch = (int)style.Stopwatch,
    };

    public static ClockStyle ToDomain(this ClockStyleEntity e) => new ClockStyle
    {
        Alignment = (ClockAlignment)e.Alignment, Shape = (DialShape)e.Shape, BorderWidth = e.BorderWidth, Markers = (MarkerStyle)e.Markers,
        EmphasizeQuarters = e.EmphasizeQuarters, Numerals = (NumeralStyle)e.Numerals, Hands = (HandStyle)e.Hands,
        HourThickness = e.HourThickness, MinuteThickness = e.MinuteThickness, SecondThickness = e.SecondThickness,
        ShowSeconds = e.ShowSeconds,
        Colors = new ClockColors(e.DialColor, e.MarkerColor, e.NumeralColor, e.HourColor, e.MinuteColor, e.SecondColor),
        Kind = Enum.IsDefined((DialKind)e.Kind) ? (DialKind)e.Kind : DialKind.Standard, DialFill = e.DialFill,
        MinuteTrack = e.MinuteTrack, Tachymeter = e.Tachymeter,
        Stopwatch = Enum.IsDefined((StopwatchDisplay)e.Stopwatch) ? (StopwatchDisplay)e.Stopwatch : StopwatchDisplay.Digital,
    }.Normalized();
}
