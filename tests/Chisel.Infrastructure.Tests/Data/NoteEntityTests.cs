using Chisel.Infrastructure.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Data;

public class NoteEntityTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Notes_AddAndSave_CanBeReadBack()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var updatedAt = new DateTimeOffset(2026, 10, 12, 16, 0, 0, TimeSpan.Zero);
        var note = new NoteEntity
        {
            Id = Guid.NewGuid(),
            Title = "Groceries",
            Body = "Buy milk and bread",
            UpdatedAt = updatedAt
        };

        // Act
        context.Notes.Add(note);
        context.SaveChanges();

        using var readContext = _fixture.CreateContext();
        var saved = readContext.Notes.First(n => n.Id == note.Id);

        // Assert
        Assert.Equal("Groceries", saved.Title);
        Assert.Equal("Buy milk and bread", saved.Body);
        Assert.Equal(updatedAt, saved.UpdatedAt);
    }

    public void Dispose() => _fixture.Dispose();
}