namespace Chisel.Infrastructure.Data;

// The one saved clock style (Id is always 1). Enums are stored as ints; colors as #RRGGBB or null for the theme color.
public class ClockStyleEntity
{
    public int Id { get; set; } = 1;
    public int Alignment { get; set; }
    public int Shape { get; set; }
    public double BorderWidth { get; set; }
    public int Markers { get; set; }
    public bool EmphasizeQuarters { get; set; }
    public int Numerals { get; set; }
    public int Hands { get; set; }
    public double HourThickness { get; set; }
    public double MinuteThickness { get; set; }
    public double SecondThickness { get; set; }
    public bool ShowSeconds { get; set; }
    public string? DialColor { get; set; }
    public string? MarkerColor { get; set; }
    public string? NumeralColor { get; set; }
    public string? HourColor { get; set; }
    public string? MinuteColor { get; set; }
    public string? SecondColor { get; set; }
    public int Kind { get; set; }
    public string? DialFill { get; set; }
    public bool MinuteTrack { get; set; }
    public bool Tachymeter { get; set; }
    public int Stopwatch { get; set; }
}
