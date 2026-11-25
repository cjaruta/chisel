namespace Chisel.Infrastructure.Data;

// The one saved set of system monitor options (Id is always 1). Shown is the SystemStats flags as an int.
public class SystemMonitorSettingsEntity
{
    public int Id { get; set; } = 1;
    public int Shown { get; set; }
    public int RefreshSeconds { get; set; }
}
