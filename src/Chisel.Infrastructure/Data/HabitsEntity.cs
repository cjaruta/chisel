namespace Chisel.Infrastructure.Data;

// The one saved habits record (Id is always 1): every habit and its history as JSON, and the format version it was written in.
public class HabitsEntity
{
    public int Id { get; set; } = 1;
    public int Version { get; set; }
    public string Json { get; set; } = "";
}
