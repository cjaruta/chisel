namespace Chisel.Infrastructure.Data;

// The one saved set of notepad options (Id is always 1).
public class NotepadSettingsEntity
{
    public int Id { get; set; } = 1;
    public Guid? LastPadId { get; set; }
    public bool Monospace { get; set; }
    public bool WordWrap { get; set; } = true;
}
