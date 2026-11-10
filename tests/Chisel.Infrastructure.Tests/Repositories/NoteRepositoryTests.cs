using Chisel.Core.Notes;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class NoteRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void GetAll_AfterAddingNotes_ReturnsThemAll()
    {
        using var context = _fixture.CreateContext();
        var repository = new NoteRepository(context, TimeProvider.System);

        repository.Add(new Note(TimeProvider.System, "First", "Body one"));
        repository.Add(new Note(TimeProvider.System, "Second", "Body two"));

        using var readContext = _fixture.CreateContext();
        var titles = new NoteRepository(readContext, TimeProvider.System).GetAll().Select(n => n.Title).ToList();

        Assert.Equal(new[] { "First", "Second" }, titles);
    }

    [Fact]
    public void Update_ChangedNote_IsSavedToTheDatabase()
    {
        using var context = _fixture.CreateContext();
        var repository = new NoteRepository(context, TimeProvider.System);
        var note = new Note(TimeProvider.System, "Groceries", "Buy milk");
        repository.Add(note);

        note.EditTitle("Shopping");
        note.EditBody("Buy milk and bread");
        repository.Update(note);

        using var readContext = _fixture.CreateContext();
        var saved = Assert.Single(new NoteRepository(readContext, TimeProvider.System).GetAll());
        Assert.Equal(note.Id, saved.Id);
        Assert.Equal("Shopping", saved.Title);
        Assert.Equal("Buy milk and bread", saved.Body);
    }

    [Fact]
    public void Update_UnknownNote_ThrowsInvalidOperationException()
    {
        using var context = _fixture.CreateContext();
        var repository = new NoteRepository(context, TimeProvider.System);

        void Act() => repository.Update(new Note(TimeProvider.System, "Ghost", "Boo"));

        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void Remove_ExistingNote_DeletesItFromTheDatabase()
    {
        using var context = _fixture.CreateContext();
        var repository = new NoteRepository(context, TimeProvider.System);
        var keep = new Note(TimeProvider.System, "Keep", "Keep body");
        var drop = new Note(TimeProvider.System, "Drop", "Drop body");
        repository.Add(keep);
        repository.Add(drop);

        repository.Remove(drop.Id);

        using var readContext = _fixture.CreateContext();
        var remaining = Assert.Single(new NoteRepository(readContext, TimeProvider.System).GetAll());
        Assert.Equal(keep.Id, remaining.Id);
    }

    [Fact]
    public void Remove_UnknownId_DoesNothing()
    {
        using var context = _fixture.CreateContext();
        var repository = new NoteRepository(context, TimeProvider.System);
        repository.Add(new Note(TimeProvider.System, "Keep", "Keep body"));

        repository.Remove(Guid.NewGuid());

        Assert.Single(repository.GetAll());
    }

    public void Dispose() => _fixture.Dispose();
}
