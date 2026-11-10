using Chisel.Core.Notes;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;

namespace Chisel.Infrastructure.Repositories;

public class NoteRepository : INoteRepository
{
    private readonly ChiselDbContext _context;
    private readonly TimeProvider _time;

    public NoteRepository(ChiselDbContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    public IReadOnlyList<Note> GetAll() =>
        _context.Notes
            .ToList()
            .Select(n => n.ToDomain(_time))
            .ToList();

    public void Add(Note note)
    {
        _context.Notes.Add(note.ToEntity());
        _context.SaveChanges();
    }

    public void Update(Note note)
    {
        var entity = _context.Notes.Find(note.Id)
            ?? throw new InvalidOperationException($"Note {note.Id} does not exist.");

        entity.Title = note.Title;
        entity.Body = note.Body;
        entity.UpdatedAt = note.UpdatedAt;
        _context.SaveChanges();
    }

    public void Remove(Guid id)
    {
        var entity = _context.Notes.Find(id);
        if (entity is null)
        {
            return;
        }

        _context.Notes.Remove(entity);
        _context.SaveChanges();
    }
}
