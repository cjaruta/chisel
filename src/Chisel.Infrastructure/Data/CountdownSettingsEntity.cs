namespace Chisel.Infrastructure.Data;

// The one saved countdown record (Id is always 1): the events and options as JSON, and the format version it was written in.
public class CountdownSettingsEntity
{
    public int Id { get; set; } = 1;
    public int Version { get; set; }
    public string Json { get; set; } = "";
}
