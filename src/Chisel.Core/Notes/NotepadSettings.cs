namespace Chisel.Core.Notes;

// What the notepad remembers between runs: the pad that was open, the monospace option and word wrap.
public sealed record NotepadSettings(Guid? LastPadId, bool Monospace, bool WordWrap)
{
    public static NotepadSettings Default { get; } = new(null, false, true);
}

// One saved set of notepad options (replace-all, like the other id-less settings).
public interface INotepadSettingsRepository
{
    NotepadSettings Get();
    void Save(NotepadSettings settings);
}
