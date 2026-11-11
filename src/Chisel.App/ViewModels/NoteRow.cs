using Chisel.Core.Notes;

namespace Chisel.App.ViewModels;

// A note as shown in the list, with its timestamp already formatted.
public sealed class NoteRow
{
    public required Note Note { get; init; }
    public required string UpdatedLabel { get; init; }

    public string Title => Note.Title;
    public string Body => Note.Body;
}
