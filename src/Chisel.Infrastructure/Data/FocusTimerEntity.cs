namespace Chisel.Infrastructure.Data;

// The one saved focus timer record (Id is always 1): its options and state as JSON, and the format version it was written in.
public class FocusTimerEntity
{
    public int Id { get; set; } = 1;
    public int Version { get; set; }
    public string Json { get; set; } = "";
}
