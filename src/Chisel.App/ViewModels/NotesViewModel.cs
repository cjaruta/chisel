using System;
using System.Collections.ObjectModel;
using Chisel.Core.Notes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class NotesViewModel : ViewModelBase
{
    private readonly INoteRepository _repository;
    private readonly TimeProvider _time;

    public ObservableCollection<Note> Notes { get; } = new();

    [ObservableProperty]
    public partial string NewNoteTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewNoteBody { get; set; } = string.Empty;

    public NotesViewModel(INoteRepository repository, TimeProvider time)
    {
        _repository = repository;
        _time = time;
        Refresh();
    }

    public void Refresh()
    {
        Notes.Clear();
        foreach (var note in _repository.GetAll())
        {
            Notes.Add(note);
        }
    }

    [RelayCommand]
    private void AddNote()
    {
        if (string.IsNullOrWhiteSpace(NewNoteTitle))
        {
            return;
        }

        _repository.Add(new Note(_time, NewNoteTitle, NewNoteBody));
        NewNoteTitle = string.Empty;
        NewNoteBody = string.Empty;
        Refresh();
    }

    [RelayCommand]
    private void RemoveNote(Note note)
    {
        _repository.Remove(note.Id);
        Refresh();
    }
}
