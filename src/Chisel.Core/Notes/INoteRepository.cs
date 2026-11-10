namespace Chisel.Core.Notes;

public interface INoteRepository
{
    IReadOnlyList<Note> GetAll();
    void Add(Note note);
    void Update(Note note);
    void Remove(Guid id);
}
