namespace Chisel.Infrastructure.Data;

// The one saved set of calculator options (Id is always 1). Angle is the AngleMode as an int.
public class CalculatorSettingsEntity
{
    public int Id { get; set; } = 1;
    public bool Advanced { get; set; }
    public int Angle { get; set; }
}
