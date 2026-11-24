namespace Chisel.Infrastructure.Data;

// One saved layout profile, keyed by its slot. The layout is kept as JSON (see LayoutProfileMapper).
public class LayoutProfileEntity
{
    public int Slot { get; set; }
    public string Name { get; set; } = "";
    public string LayoutJson { get; set; } = "";
}
