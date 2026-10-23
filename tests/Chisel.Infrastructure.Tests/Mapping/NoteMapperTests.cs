using Chisel.Core.Notes;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;
using Xunit;

namespace Chisel.Infrastructure.Tests.Mapping;

public class NoteMapperTests
{
    [Fact]
    public void ToEntity_CopiesIdTitleBodyAndUpdatedAt()
    {
        var updatedAt = new DateTimeOffset(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);
        var note = new Note(TimeProvider.System, Guid.NewGuid(), "Groceries", "Buy milk", updatedAt);

        var entity = note.ToEntity();

        Assert.Equal(note.Id, entity.Id);
        Assert.Equal("Groceries", entity.Title);
        Assert.Equal("Buy milk", entity.Body);
        Assert.Equal(updatedAt, entity.UpdatedAt);
    }

    [Fact]
    public void ToDomain_CopiesIdTitleBodyAndUpdatedAt()
    {
        var updatedAt = new DateTimeOffset(2026, 9, 17, 8, 0, 0, TimeSpan.Zero);
        var entity = new NoteEntity { Id = Guid.NewGuid(), Title = "Groceries", Body = "Buy milk", UpdatedAt = updatedAt };

        var note = entity.ToDomain(TimeProvider.System);

        Assert.Equal(entity.Id, note.Id);
        Assert.Equal("Groceries", note.Title);
        Assert.Equal("Buy milk", note.Body);
        Assert.Equal(updatedAt, note.UpdatedAt);
        Assert.Equal(2, note.WordCount);
    }
}