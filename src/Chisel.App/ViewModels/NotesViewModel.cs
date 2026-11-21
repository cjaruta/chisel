using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Chisel.App.Services;
using Chisel.Core.Notes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// The notepad: one editable plain-text pad at a time. Each pad is a Note (title = pad name), so notes that already
// exist are simply the first pads. Typing is saved by itself after a quiet second (Tick), and at once by Flush, which
// is called when switching pads, hiding the widget and closing the app.
public partial class NotesViewModel : ViewModelBase
{
    private const string DefaultName = "Untitled";
    private static readonly TimeSpan AutosaveDelay = TimeSpan.FromSeconds(1);

    private readonly INoteRepository _repository;
    private readonly TimeProvider _time;
    private readonly INotepadSettingsRepository? _settings;
    private readonly INotepadFiles? _files;

    private bool _loading = true;
    private bool _dirty;
    private DateTimeOffset _lastEdit;

    public NotesViewModel(INoteRepository repository, TimeProvider time,
        INotepadSettingsRepository? settings = null, INotepadFiles? files = null)
    {
        _repository = repository;
        _time = time;
        _settings = settings;
        _files = files;

        var saved = settings?.Get() ?? NotepadSettings.Default;
        Monospace = saved.Monospace;
        WordWrap = saved.WordWrap;

        var pads = Sorted(repository.GetAll());
        if (pads.Count == 0)
        {
            var blank = new Note(time, DefaultName, "");
            repository.Add(blank);
            pads = Sorted(repository.GetAll());
            if (pads.Count == 0)
            {
                pads = [blank];
            }
        }
        Pads = pads;
        SelectedPad = pads.FirstOrDefault(p => p.Id == saved.LastPadId) ?? pads[0];
        Text = SelectedPad.Body;
        _loading = false;
    }

    // The pads, sorted by name.
    [ObservableProperty]
    public partial IReadOnlyList<Note> Pads { get; private set; }

    // The pad being edited. Setting it saves the pending edits to the old pad first.
    [ObservableProperty]
    public partial Note? SelectedPad { get; set; }

    // The editor's text: the selected pad's whole body.
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool Monospace { get; set; }

    [ObservableProperty]
    public partial bool WordWrap { get; set; } = true;

    [ObservableProperty]
    public partial bool IsRenaming { get; private set; }

    [ObservableProperty]
    public partial string RenameText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; private set; }

    // A short message when a file action failed; null otherwise.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; private set; }

    public bool HasNotice => Notice is not null;

    // Whether the file actions can do anything (the app gives the pickers; tests may not).
    public bool HasFiles => _files is not null;

    private static List<Note> Sorted(IEnumerable<Note> notes) =>
        notes.OrderBy(n => n.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(n => n.Id).ToList();

    partial void OnSelectedPadChanging(Note? oldValue, Note? newValue)
    {
        if (!_loading)
        {
            Flush();
        }
    }

    partial void OnSelectedPadChanged(Note? value)
    {
        if (_loading || value is null)
        {
            return;
        }
        IsRenaming = false;
        IsConfirmingDelete = false;
        LoadText(value.Body);
        SaveSettings();
    }

    partial void OnTextChanged(string value)
    {
        if (!_loading)
        {
            _dirty = true;
            _lastEdit = _time.GetUtcNow();
        }
    }

    partial void OnMonospaceChanged(bool value) => SaveSettings();

    partial void OnWordWrapChanged(bool value) => SaveSettings();

    private void LoadText(string body)
    {
        var was = _loading;
        _loading = true;
        Text = body;
        _dirty = false;
        _loading = was;
    }

    private void SaveSettings()
    {
        if (!_loading)
        {
            _settings?.Save(new NotepadSettings(SelectedPad?.Id, Monospace, WordWrap));
        }
    }

    // Called on every app tick: saves typing that has been quiet for a second.
    public void Tick()
    {
        if (_dirty && _time.GetUtcNow() - _lastEdit >= AutosaveDelay)
        {
            Flush();
        }
    }

    // Saves any pending edits to the current pad now.
    public void Flush()
    {
        if (!_dirty || SelectedPad is not { } pad)
        {
            return;
        }
        _dirty = false;
        pad.EditBody(Text);
        _repository.Update(pad);
    }

    private void Reload(Note? select)
    {
        var was = _loading;
        _loading = true;
        Pads = Sorted(_repository.GetAll());
        SelectedPad = select is null ? Pads[0] : Pads.First(p => p.Id == select.Id);
        _loading = was;
    }

    // Adds a pad and opens it. The current pad's edits are saved first.
    private void AddPad(string title, string body)
    {
        Flush();
        var pad = new Note(_time, title, body);
        _repository.Add(pad);
        Reload(pad);
        IsRenaming = false;
        IsConfirmingDelete = false;
        LoadText(body);
        _dirty = false;
        SaveSettings();
    }

    private string NextName()
    {
        var names = _repository.GetAll().Select(n => n.Title).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!names.Contains(DefaultName))
        {
            return DefaultName;
        }
        for (var i = 2; ; i++)
        {
            if (!names.Contains($"{DefaultName} {i}"))
            {
                return $"{DefaultName} {i}";
            }
        }
    }

    [RelayCommand]
    private void NewPad()
    {
        Notice = null;
        AddPad(NextName(), "");
    }

    [RelayCommand]
    private void BeginRename()
    {
        IsConfirmingDelete = false;
        RenameText = SelectedPad?.Title ?? string.Empty;
        IsRenaming = SelectedPad is not null;
    }

    [RelayCommand]
    private void CommitRename()
    {
        if (!IsRenaming || SelectedPad is not { } pad)
        {
            return;
        }
        var name = RenameText.Trim();
        pad.EditTitle(name.Length == 0 ? DefaultName : name);
        _repository.Update(pad);
        IsRenaming = false;
        Reload(pad);
    }

    [RelayCommand]
    private void CancelRename() => IsRenaming = false;

    [RelayCommand]
    private void RequestDelete()
    {
        IsRenaming = false;
        IsConfirmingDelete = SelectedPad is not null;
    }

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    // Deletes the pad for good; its unsaved typing goes with it. The next pad in the list opens (a fresh blank one
    // when it was the last).
    [RelayCommand]
    private void ConfirmDelete()
    {
        if (!IsConfirmingDelete || SelectedPad is not { } pad)
        {
            return;
        }
        var index = Pads.ToList().FindIndex(p => p.Id == pad.Id);
        _dirty = false;
        IsConfirmingDelete = false;
        _repository.Remove(pad.Id);
        if (_repository.GetAll().Count == 0)
        {
            _repository.Add(new Note(_time, DefaultName, ""));
        }
        var remaining = Sorted(_repository.GetAll());
        var next = remaining[Math.Min(index, remaining.Count - 1)];
        Reload(next);
        LoadText(next.Body);
        SaveSettings();
    }

    // Imports a copy of a text file as a new pad. The file itself is never written to.
    [RelayCommand(CanExecute = nameof(HasFiles))]
    private async Task OpenTextFile()
    {
        Notice = null;
        try
        {
            if (await _files!.OpenAsync() is not { } file)
            {
                return;
            }
            var name = Path.GetFileNameWithoutExtension(file.Name).Trim();
            AddPad(name.Length == 0 ? "Imported text" : name, file.Text);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Notice = $"Could not open the file: {e.Message}";
        }
    }

    // Exports the whole current pad (pending edits included) to a text file of the user's choosing.
    [RelayCommand(CanExecute = nameof(HasFiles))]
    private async Task SaveAsTextFile()
    {
        Notice = null;
        Flush();
        try
        {
            await _files!.SaveAsync(FileNameFor(SelectedPad?.Title), Text);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notice = $"Could not save the file: {e.Message}";
        }
    }

    [RelayCommand]
    private void DismissNotice() => Notice = null;

    private static string FileNameFor(string? title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string((title ?? "").Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return (name.Length == 0 ? DefaultName : name) + ".txt";
    }
}
