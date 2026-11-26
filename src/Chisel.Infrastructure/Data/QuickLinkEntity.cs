namespace Chisel.Infrastructure.Data;

public class QuickLinkEntity
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    // A web address or a full path (the column keeps its old name).
    public string Url { get; set; } = string.Empty;
    public int Position { get; set; }
}