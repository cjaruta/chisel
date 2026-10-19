namespace Chisel.Infrastructure.Data;

public class UserSettingsEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeOnly WakeTime { get; set; }
    public TimeOnly BedTime { get; set; }
}