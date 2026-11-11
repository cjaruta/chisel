using Chisel.App.ViewModels;
using Chisel.Core.Notes;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class NotesViewModelTests
{
    private sealed class FakeNoteRepository : INoteRepository
    {
        private readonly List<Note> _items = new();

        public IReadOnlyList<Note> GetAll() => _items;

        public void Add(Note note) => _items.Add(note);

        public void Update(Note note)
        {
            var index = _items.FindIndex(n => n.Id == note.Id);
            if (index >= 0)
            {
                _items[index] = note;
            }
        }

        public void Remove(Guid id) => _items.RemoveAll(n => n.Id == id);
    }

    [Fact]
    public void Constructor_LoadsExistingNotes()
    {
        var repository = new FakeNoteRepository();
        var time = new FakeTimeProvider();
        repository.Add(new Note(time, "Groceries", "Buy milk"));

        var viewModel = new NotesViewModel(repository, time);

        Assert.Equal(new[] { "Groceries" }, viewModel.Notes.Select(n => n.Title).ToArray());
    }

    [Fact]
    public void SaveNote_WithTitleAndNoEditingNote_AddsToRepositoryAndClearsInputs()
    {
        var repository = new FakeNoteRepository();
        var time = new FakeTimeProvider();
        var viewModel = new NotesViewModel(repository, time)
        {
            NewNoteTitle = "Groceries",
            NewNoteBody = "Buy milk"
        };

        viewModel.SaveNoteCommand.Execute(null);

        Assert.Equal(new[] { "Groceries" }, repository.GetAll().Select(n => n.Title).ToArray());
        Assert.Equal(string.Empty, viewModel.NewNoteTitle);
        Assert.Equal(string.Empty, viewModel.NewNoteBody);
    }

    [Fact]
    public void SaveNote_WithBlankTitle_DoesNothing()
    {
        var repository = new FakeNoteRepository();
        var time = new FakeTimeProvider();
        var viewModel = new NotesViewModel(repository, time) { NewNoteTitle = "   " };

        viewModel.SaveNoteCommand.Execute(null);

        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void SelectNote_LoadsItIntoTheEditFields()
    {
        var repository = new FakeNoteRepository();
        var time = new FakeTimeProvider();
        var note = new Note(time, "Groceries", "Buy milk");
        repository.Add(note);
        var viewModel = new NotesViewModel(repository, time);

        viewModel.SelectNoteCommand.Execute(note);

        Assert.Equal("Groceries", viewModel.NewNoteTitle);
        Assert.Equal("Buy milk", viewModel.NewNoteBody);
        Assert.Equal(note.Id, viewModel.EditingNoteId);
    }

    [Fact]
    public void SaveNote_WhileEditingAnExistingNote_UpdatesItInsteadOfAddingANewOne()
    {
        var repository = new FakeNoteRepository();
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero));
        var note = new Note(time, "Groceries", "Buy milk");
        repository.Add(note);
        var viewModel = new NotesViewModel(repository, time);
        viewModel.SelectNoteCommand.Execute(note);

        time.SetUtcNow(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
        viewModel.NewNoteTitle = "Shopping";
        viewModel.NewNoteBody = "Buy milk and bread";
        viewModel.SaveNoteCommand.Execute(null);

        var saved = Assert.Single(repository.GetAll());
        Assert.Equal(note.Id, saved.Id);
        Assert.Equal("Shopping", saved.Title);
        Assert.Equal("Buy milk and bread", saved.Body);
        Assert.Equal(time.GetUtcNow(), saved.UpdatedAt);
        Assert.Null(viewModel.EditingNoteId);
        Assert.Equal(string.Empty, viewModel.NewNoteTitle);
    }

    [Fact]
    public void CancelEdit_ClearsEditingStateAndInputs()
    {
        var repository = new FakeNoteRepository();
        var time = new FakeTimeProvider();
        var note = new Note(time, "Groceries", "Buy milk");
        repository.Add(note);
        var viewModel = new NotesViewModel(repository, time);
        viewModel.SelectNoteCommand.Execute(note);

        viewModel.CancelEditCommand.Execute(null);

        Assert.Null(viewModel.EditingNoteId);
        Assert.Equal(string.Empty, viewModel.NewNoteTitle);
        Assert.Equal(string.Empty, viewModel.NewNoteBody);
    }

    [Fact]
    public void RemoveNote_ExistingNote_RemovesFromRepository()
    {
        var repository = new FakeNoteRepository();
        var time = new FakeTimeProvider();
        var note = new Note(time, "Groceries", "Buy milk");
        repository.Add(note);
        var viewModel = new NotesViewModel(repository, time);

        viewModel.RemoveNoteCommand.Execute(note);

        Assert.Empty(repository.GetAll());
        Assert.Empty(viewModel.Notes);
    }
}
