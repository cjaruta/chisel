namespace Chisel.Infrastructure.Data;

public class TaskEntity
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsDone { get; set; }
}