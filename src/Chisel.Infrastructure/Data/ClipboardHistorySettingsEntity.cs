namespace Chisel.Infrastructure.Data;

// The one saved set of clipboard history choices (Id is always 1). Never holds clipboard text: saved history, when the user turns
// it on, goes to its own file (ClipboardHistoryFileStore) so it can be deleted outright.
public class ClipboardHistorySettingsEntity
{
    public int Id { get; set; } = 1;
    public bool KeepHistory { get; set; }
    public int MaxItems { get; set; }
    public int MaxLength { get; set; }
}
