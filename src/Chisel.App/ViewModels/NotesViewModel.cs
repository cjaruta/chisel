using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Chisel.Core.Notes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class NotesViewModel : ViewModelBase
{
    private readonly INoteRepository _repository;
    private readonly TimeProvider _time;

    private readonly TimeDisplay _display;

    public ObservableCollection<NoteRow> Notes { get; } = new();

    [ObservableProperty]
    public partial string NewNoteTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewNoteBody { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Guid? EditingNoteId { get; set; }

    public NotesViewModel(INoteRepository repository, TimeProvider time, TimeDisplay? display = null)
    {
        _repository = repository;
        _time = time;
        _display = display ?? new TimeDisplay();
        _display.PropertyChanged += (_, _) => Refresh();
        Refresh();
    }

    public void Refresh()
    {
        Notes.Clear();
        foreach (var note in _repository.GetAll())
        {
            Notes.Add(new NoteRow { Note = note, UpdatedLabel = FormatUpdated(note.UpdatedAt) });
        }
    }

    private string FormatUpdated(DateTimeOffset updatedAt)
    {
        var local = TimeZoneInfo.ConvertTime(updatedAt, _time.LocalTimeZone);
        var date = local.ToString("MMM d", CultureInfo.InvariantCulture);
        return $"Updated {date}, {_display.Time(TimeOnly.FromDateTime(local.DateTime))}";
    }

    [RelayCommand]
    private void SelectNote(Note note)
    {
        EditingNoteId = note.Id;
        NewNoteTitle = note.Title;
        NewNoteBody = note.Body;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EditingNoteId = null;
        NewNoteTitle = string.Empty;
        NewNoteBody = string.Empty;
    }

    [RelayCommand]
    private void SaveNote()
    {
        if (string.IsNullOrWhiteSpace(NewNoteTitle))
        {
            return;
        }

        if (EditingNoteId is { } id)
        {
            var existing = Notes.FirstOrDefault(n => n.Note.Id == id)?.Note;
            if (existing is not null)
            {
                existing.EditTitle(NewNoteTitle);
                existing.EditBody(NewNoteBody);
                _repository.Update(existing);
            }
        }
        else
        {
            _repository.Add(new Note(_time, NewNoteTitle, NewNoteBody));
        }

        EditingNoteId = null;
        NewNoteTitle = string.Empty;
        NewNoteBody = string.Empty;
        Refresh();
    }

    [RelayCommand]
    private void RemoveNote(Note note)
    {
        if (EditingNoteId == note.Id)
        {
            EditingNoteId = null;
            NewNoteTitle = string.Empty;
            NewNoteBody = string.Empty;
        }

        _repository.Remove(note.Id);
        Refresh();
    }
}
