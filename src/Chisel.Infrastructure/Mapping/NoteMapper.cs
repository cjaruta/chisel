using Chisel.Core.Notes;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Mapping;

public static class NoteMapper
{
    public static NoteEntity ToEntity(this Note note) => new()
    {
        Id = note.Id,
        Title = note.Title,
        Body = note.Body,
        UpdatedAt = note.UpdatedAt
    };

    public static Note ToDomain(this NoteEntity entity, TimeProvider time) =>
        new(time, entity.Id, entity.Title, entity.Body, entity.UpdatedAt);
}