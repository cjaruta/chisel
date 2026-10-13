namespace Chisel.Infrastructure.Data;

public class NoteEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}