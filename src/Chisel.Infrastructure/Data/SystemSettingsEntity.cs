namespace Chisel.Infrastructure.Data;

// The one saved set of app-level choices (Id is always 1).
public class SystemSettingsEntity
{
    public int Id { get; set; } = 1;
    public bool CloseToTray { get; set; }
    public bool StartMinimized { get; set; }
    public string Hotkey { get; set; } = "";
}
