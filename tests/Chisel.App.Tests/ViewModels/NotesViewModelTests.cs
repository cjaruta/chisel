using Chisel.App.Services;
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
        public int Updates { get; private set; }

        public IReadOnlyList<Note> GetAll() => _items.ToList();
        public void Add(Note note) => _items.Add(note);

        public void Update(Note note)
        {
            Updates++;
            var index = _items.FindIndex(n => n.Id == note.Id);
            if (index >= 0)
            {
                _items[index] = note;
            }
        }

        public void Remove(Guid id) => _items.RemoveAll(n => n.Id == id);
    }

    private sealed class FakeSettings : INotepadSettingsRepository
    {
        public NotepadSettings Current { get; set; } = NotepadSettings.Default;
        public NotepadSettings Get() => Current;
        public void Save(NotepadSettings settings) => Current = settings;
    }

    private sealed class FakeFiles : INotepadFiles
    {
        public NotepadFile? ToOpen { get; set; }
        public Exception? OpenFails { get; set; }
        public bool SaveResult { get; set; } = true;
        public Exception? SaveFails { get; set; }
        public List<(string Name, string Text)> Saved { get; } = new();

        public Task<NotepadFile?> OpenAsync() => OpenFails is { } e ? Task.FromException<NotepadFile?>(e) : Task.FromResult(ToOpen);

        public Task<bool> SaveAsync(string suggestedName, string text)
        {
            if (SaveFails is { } e)
            {
                return Task.FromException<bool>(e);
            }
            if (SaveResult)
            {
                Saved.Add((suggestedName, text));
            }
            return Task.FromResult(SaveResult);
        }
    }

    private readonly FakeTimeProvider _time = new();
    private readonly FakeNoteRepository _repository = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeFiles _files = new();

    private NotesViewModel Create() => new(_repository, _time, _settings, _files);

    private void Seed(string title, string body) => _repository.Add(new Note(_time, title, body));

    private static string[] Names(NotesViewModel vm) => vm.Pads.Select(p => p.Title).ToArray();

    private string BodyOf(string title) => _repository.GetAll().Single(n => n.Title == title).Body;

    [Fact]
    public void ExistingNotes_BecomePads_OneToOne_WithTitlesAndBodiesKept()
    {
        Seed("Groceries", "Buy milk\n\n  and eggs");
        Seed("Ideas", "Ünïcödé ✓");

        var vm = Create();

        Assert.Equal(new[] { "Groceries", "Ideas" }, Names(vm));
        Assert.Equal(2, _repository.GetAll().Count);
        Assert.Equal("Buy milk\n\n  and eggs", vm.Pads.Single(p => p.Title == "Groceries").Body);
        vm.SelectedPad = vm.Pads.Single(p => p.Title == "Ideas");
        Assert.Equal("Ünïcödé ✓", vm.Text);
    }

    [Fact]
    public void WithNoNotes_StartsWithOneBlankPad_AndTypingNeedsNoTitle()
    {
        var vm = Create();

        Assert.Single(vm.Pads);
        Assert.Equal("Untitled", vm.SelectedPad!.Title);
        Assert.Equal("", vm.Text);

        vm.Text = "first thought";
        vm.Flush();

        Assert.Equal("first thought", BodyOf("Untitled"));
    }

    [Fact]
    public void RestoresTheLastUsedPad_AndFallsBackToTheFirstWhenItIsGone()
    {
        Seed("A", "a");
        Seed("B", "b");
        var b = _repository.GetAll().Single(n => n.Title == "B");
        _settings.Current = _settings.Current with { LastPadId = b.Id };

        Assert.Equal("B", Create().SelectedPad!.Title);

        _settings.Current = _settings.Current with { LastPadId = Guid.NewGuid() };
        Assert.Equal("A", Create().SelectedPad!.Title);
    }

    [Fact]
    public void Selecting_APad_RemembersItAsTheLastUsed()
    {
        Seed("A", "a");
        Seed("B", "b");
        var vm = Create();

        vm.SelectedPad = vm.Pads.Single(p => p.Title == "B");

        Assert.Equal(vm.SelectedPad.Id, _settings.Current.LastPadId);
    }

    [Fact]
    public void Autosave_WritesAfterAQuietSecond_NotBefore()
    {
        var vm = Create();
        vm.Text = "draft";

        _time.Advance(TimeSpan.FromMilliseconds(500));
        vm.Tick();
        Assert.Equal("", BodyOf("Untitled"));

        _time.Advance(TimeSpan.FromMilliseconds(600));
        vm.Tick();
        Assert.Equal("draft", BodyOf("Untitled"));
    }

    [Fact]
    public void KeepingOnTyping_PostponesTheAutosave()
    {
        var vm = Create();
        vm.Text = "a";
        _time.Advance(TimeSpan.FromMilliseconds(800));
        vm.Text = "ab";
        _time.Advance(TimeSpan.FromMilliseconds(800));
        vm.Tick();

        Assert.Equal("", BodyOf("Untitled"));
    }

    [Fact]
    public void Flush_WritesOnlyWhenSomethingChanged()
    {
        var vm = Create();
        vm.Flush();
        Assert.Equal(0, _repository.Updates);

        vm.Text = "x";
        vm.Flush();
        vm.Flush();

        Assert.Equal(1, _repository.Updates);
    }

    [Fact]
    public void SwitchingPads_SavesPendingEditsToThePadTheyWereTypedIn()
    {
        Seed("A", "a");
        Seed("B", "b");
        var vm = Create();
        vm.Text = "a edited";

        vm.SelectedPad = vm.Pads.Single(p => p.Title == "B");

        Assert.Equal("a edited", BodyOf("A"));
        Assert.Equal("b", vm.Text);
        Assert.Equal("b", BodyOf("B"));
    }

    [Fact]
    public void LoadingAPad_IsNotAnEdit_SoNothingIsWritten()
    {
        Seed("A", "a");
        Seed("B", "b");
        var vm = Create();

        vm.SelectedPad = vm.Pads.Single(p => p.Title == "B");
        vm.Flush();

        Assert.Equal(0, _repository.Updates);
    }

    [Fact]
    public void NewPad_SavesTheCurrentOne_AddsABlankPad_AndSelectsIt()
    {
        var vm = Create();
        vm.Text = "keep me";

        vm.NewPadCommand.Execute(null);
        vm.NewPadCommand.Execute(null);

        Assert.Equal("keep me", BodyOf("Untitled"));
        Assert.Equal(new[] { "Untitled", "Untitled 2", "Untitled 3" }, Names(vm));
        Assert.Equal("Untitled 3", vm.SelectedPad!.Title);
        Assert.Equal("", vm.Text);
    }

    [Fact]
    public void Rename_ChangesTheTitle_AndKeepsPendingText()
    {
        Seed("Old", "body");
        var vm = Create();
        vm.Text = "body plus";

        vm.BeginRenameCommand.Execute(null);
        Assert.True(vm.IsRenaming);
        Assert.Equal("Old", vm.RenameText);
        vm.RenameText = "  New name ";
        vm.CommitRenameCommand.Execute(null);

        Assert.False(vm.IsRenaming);
        Assert.Equal("New name", vm.SelectedPad!.Title);
        Assert.Equal(new[] { "New name" }, Names(vm));
        Assert.Equal("body plus", vm.Text);
        vm.Flush();
        Assert.Equal("body plus", BodyOf("New name"));
    }

    [Fact]
    public void Rename_WithABlankName_FallsBackToUntitled_AndCancelChangesNothing()
    {
        Seed("Keep", "");
        var vm = Create();

        vm.BeginRenameCommand.Execute(null);
        vm.RenameText = "   ";
        vm.CommitRenameCommand.Execute(null);
        Assert.Equal("Untitled", vm.SelectedPad!.Title);

        vm.BeginRenameCommand.Execute(null);
        vm.RenameText = "Nope";
        vm.CancelRenameCommand.Execute(null);
        Assert.Equal("Untitled", vm.SelectedPad.Title);
        Assert.False(vm.IsRenaming);
    }

    [Fact]
    public void Delete_AsksFirst_AndCancelKeepsThePad()
    {
        Seed("A", "a");
        Seed("B", "b");
        var vm = Create();

        vm.RequestDeleteCommand.Execute(null);
        Assert.True(vm.IsConfirmingDelete);
        Assert.Equal(2, vm.Pads.Count);

        vm.CancelDeleteCommand.Execute(null);

        Assert.False(vm.IsConfirmingDelete);
        Assert.Equal(2, _repository.GetAll().Count);
    }

    [Fact]
    public void ConfirmedDelete_RemovesThePad_DiscardsItsPendingText_AndOpensAnother()
    {
        Seed("A", "a");
        Seed("B", "b");
        var vm = Create();
        vm.Text = "typed into A";

        vm.RequestDeleteCommand.Execute(null);
        vm.ConfirmDeleteCommand.Execute(null);

        Assert.Equal(new[] { "B" }, Names(vm));
        Assert.Equal("B", vm.SelectedPad!.Title);
        Assert.Equal("b", vm.Text);
        Assert.False(vm.IsConfirmingDelete);
        vm.Flush();
        Assert.Equal("b", BodyOf("B"));
        Assert.Single(_repository.GetAll());
    }

    [Fact]
    public void DeletingTheLastPad_LeavesOneFreshBlankPad()
    {
        Seed("Only", "text");
        var vm = Create();

        vm.RequestDeleteCommand.Execute(null);
        vm.ConfirmDeleteCommand.Execute(null);

        Assert.Equal(new[] { "Untitled" }, Names(vm));
        Assert.Equal("", vm.Text);
    }

    [Fact]
    public async Task OpenTextFile_ImportsACopyAsANewPad_PreservingEveryCharacter()
    {
        Seed("Mine", "mine");
        var text = "line one\r\n\r\n    indented\r\n\ttabbed\r\nünï ✓ 日本語 😀\r\n";
        _files.ToOpen = new NotepadFile("todo list.txt", text);
        var vm = Create();
        vm.Text = "mine edited";

        await vm.OpenTextFileCommand.ExecuteAsync(null);

        Assert.Equal("todo list", vm.SelectedPad!.Title);
        Assert.Equal(text, vm.Text);
        vm.Flush();
        Assert.Equal(text, BodyOf("todo list"));
        Assert.Equal("mine edited", BodyOf("Mine"));
        Assert.Equal(2, _repository.GetAll().Count);
        Assert.Empty(_files.Saved);
    }

    [Fact]
    public async Task EditingAnImportedPad_NeverWritesBackToTheFile()
    {
        _files.ToOpen = new NotepadFile("a.txt", "x");
        var vm = Create();
        await vm.OpenTextFileCommand.ExecuteAsync(null);

        vm.Text = "changed";
        vm.Flush();

        Assert.Empty(_files.Saved);
    }

    [Fact]
    public async Task CancellingTheOpenPicker_ChangesNothing()
    {
        Seed("Mine", "mine");
        var vm = Create();
        vm.Text = "pending";
        _files.ToOpen = null;

        await vm.OpenTextFileCommand.ExecuteAsync(null);

        Assert.Single(vm.Pads);
        Assert.Equal("pending", vm.Text);
        Assert.Null(vm.Notice);
    }

    [Fact]
    public async Task AFailedOpen_ShowsANotice_AndKeepsTheEditsAndPads()
    {
        Seed("Mine", "mine");
        var vm = Create();
        vm.Text = "pending";
        _files.OpenFails = new IOException("disk gone");

        await vm.OpenTextFileCommand.ExecuteAsync(null);

        Assert.Contains("disk gone", vm.Notice);
        Assert.Equal("pending", vm.Text);
        Assert.Single(vm.Pads);
        vm.Flush();
        Assert.Equal("pending", BodyOf("Mine"));
    }

    [Fact]
    public async Task SaveAsTextFile_ExportsTheWholePad_IncludingUnsavedEdits()
    {
        Seed("Trip: Day 1/2", "old");
        var vm = Create();
        vm.Text = "line 1\r\n\r\n  line 3 ✓";

        await vm.SaveAsTextFileCommand.ExecuteAsync(null);

        var saved = Assert.Single(_files.Saved);
        Assert.Equal("line 1\r\n\r\n  line 3 ✓", saved.Text);
        Assert.Equal("Trip_ Day 1_2.txt", saved.Name);
        Assert.Null(vm.Notice);
        Assert.Equal("line 1\r\n\r\n  line 3 ✓", BodyOf("Trip: Day 1/2"));
    }

    [Fact]
    public async Task CancellingTheSavePicker_ChangesNothing()
    {
        var vm = Create();
        vm.Text = "pending";
        _files.SaveResult = false;

        await vm.SaveAsTextFileCommand.ExecuteAsync(null);

        Assert.Empty(_files.Saved);
        Assert.Null(vm.Notice);
        Assert.Equal("pending", vm.Text);
    }

    [Fact]
    public async Task AFailedSave_ShowsANotice_AndLosesNoEdits()
    {
        var vm = Create();
        vm.Text = "precious";
        _files.SaveFails = new UnauthorizedAccessException("read-only");

        await vm.SaveAsTextFileCommand.ExecuteAsync(null);

        Assert.Contains("read-only", vm.Notice);
        Assert.Equal("precious", vm.Text);
        vm.Flush();
        Assert.Equal("precious", BodyOf("Untitled"));

        vm.DismissNoticeCommand.Execute(null);
        Assert.Null(vm.Notice);
    }

    [Fact]
    public void MonospaceAndWordWrap_AreRememberedBetweenRuns()
    {
        var vm = Create();
        Assert.True(vm.WordWrap);
        Assert.False(vm.Monospace);

        vm.Monospace = true;
        vm.WordWrap = false;

        var again = Create();
        Assert.True(again.Monospace);
        Assert.False(again.WordWrap);
    }
}
