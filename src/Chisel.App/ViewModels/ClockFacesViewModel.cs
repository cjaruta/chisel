using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Chisel.App.Services;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// One face in the picker: a built-in look (data in ClockPresets, never changed) or one the user saved.
public sealed partial class ClockFaceItem : ObservableObject
{
    internal ClockFaceItem(string title, ClockStyle style, ClockSlotsConfig? slots, Guid? id, string presetId = "")
    {
        Title = title;
        Style = style;
        Slots = slots;
        Id = id;
        PresetId = presetId;
    }

    public string Title { get; }
    public ClockStyle Style { get; }

    // What the face puts in the widget's slots; null for a built-in face, which leaves the layout as it is.
    public ClockSlotsConfig? Slots { get; }

    // The saved face's id; null for a built-in one.
    public Guid? Id { get; }
    internal string PresetId { get; }
    public bool IsBuiltIn => Id is null;

    // Whether this is the look the widget wears now.
    [ObservableProperty]
    public partial bool IsApplied { get; internal set; }
}

// The face picker on the Clock page: the built-in faces next to the ones the user saved, and what can be done with a saved one
// (save the current look and layout as new, apply, rename, update, duplicate, delete, copy out and paste in). Every change is saved
// at once. A saved face keeps the whole style (dial, markers, numerals, hands, colors, seconds, alignment) and the widget's slot
// layout; the 12/24-hour setting is not part of it, because it belongs to the whole app.
public partial class ClockFacesViewModel : ViewModelBase
{
    private readonly IClockFaceStore _store;
    private readonly ClockSettingsViewModel _settings;
    private readonly ClockToolsViewModel _tools;
    private readonly ClockFaceLibrary _library;

    public IReadOnlyList<ClockFaceItem> BuiltIn { get; }

    public ObservableCollection<ClockFaceItem> Custom { get; } = new();

    // The face being managed (the one last chosen or saved).
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameSelectedCommand), nameof(UpdateSelectedCommand), nameof(DeleteSelectedCommand), nameof(DuplicateSelectedCommand), nameof(ApplySelectedCommand))]
    [NotifyPropertyChangedFor(nameof(CanManage))]
    public partial ClockFaceItem? Selected { get; set; }

    // The name box: the name for a new face, or the new name for a rename.
    [ObservableProperty]
    public partial string NewName { get; set; } = string.Empty;

    // What went wrong with the last action, or null.
    [ObservableProperty]
    public partial string? Error { get; private set; }

    public bool CanManage => Selected is { IsBuiltIn: false };

    public bool HasCustom => Custom.Count > 0;

    public ClockFacesViewModel(IClockFaceStore? store, ClockSettingsViewModel settings, ClockToolsViewModel tools)
    {
        _store = store ?? new MemoryClockFaceStore();
        _settings = settings;
        _tools = tools;
        _library = new ClockFaceLibrary(_store.Load());
        BuiltIn = ClockPresets.All.Select(p => new ClockFaceItem(p.Title, p.Style, null, null, p.Id)).ToList();
        Rebuild(null);
        _settings.PropertyChanged += OnLookChanged;
        _tools.PropertyChanged += OnLookChanged;
        MarkApplied();
    }

    private void OnLookChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClockSettingsViewModel.Style) or nameof(ClockToolsViewModel.Slots))
        {
            MarkApplied();
        }
    }

    // Marks the faces whose look the widget wears now (a built-in one by its look alone, a saved one by look and layout).
    private void MarkApplied()
    {
        var presetId = ClockPresets.IdOf(_settings.Style);
        foreach (var face in BuiltIn)
        {
            face.IsApplied = face.PresetId == presetId;
        }
        foreach (var face in Custom)
        {
            face.IsApplied = face.Style == _settings.Style && face.Slots == _tools.Slots;
        }
    }

    private void Rebuild(Guid? keep)
    {
        Custom.Clear();
        foreach (var face in _library.Faces)
        {
            Custom.Add(new ClockFaceItem(face.Name, face.Style, face.Slots, face.Id));
        }
        Selected = keep is { } id ? Custom.FirstOrDefault(f => f.Id == id) : null;
        OnPropertyChanged(nameof(HasCustom));
        MarkApplied();
    }

    // Records the result of a library change: saves the list, or shows what was wrong.
    private bool Finish(ClockFaceResult result, Guid? select)
    {
        if (!result.IsOk)
        {
            Error = result.Error;
            return false;
        }
        Error = null;
        _store.Save(_library.Faces);
        Rebuild(select);
        return true;
    }

    [RelayCommand]
    private void SaveAsNew()
    {
        var result = _library.SaveAsNew(NewName, _settings.Style, _tools.Slots);
        if (Finish(result, result.Face?.Id))
        {
            NewName = string.Empty;
        }
    }

    private bool CanChangeSelected() => CanManage;

    [RelayCommand(CanExecute = nameof(CanChangeSelected))]
    private void RenameSelected()
    {
        if (Selected is { Id: { } id } && Finish(_library.Rename(id, NewName), id))
        {
            NewName = string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeSelected))]
    private void UpdateSelected()
    {
        if (Selected is { Id: { } id })
        {
            Finish(_library.Update(id, _settings.Style, _tools.Slots), id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeSelected))]
    private void DeleteSelected()
    {
        if (Selected is { Id: { } id })
        {
            Finish(_library.Delete(id), null);
        }
    }

    // A copy of the selected face; a built-in one becomes a saved face with its look and the current layout.
    [RelayCommand]
    private void DuplicateSelected()
    {
        switch (Selected)
        {
            case { Id: { } id }:
                var copy = _library.Duplicate(id);
                Finish(copy, copy.Face?.Id);
                break;
            case { } builtIn:
                var name = $"{builtIn.Title} copy";
                for (var n = 2; _library.CheckName(name) is not null && n < 1000; n++)
                {
                    name = $"{builtIn.Title} copy {n}";
                }
                var made = _library.SaveAsNew(name, ClockPresets.Apply(builtIn.PresetId, _settings.Style), _tools.Slots);
                Finish(made, made.Face?.Id);
                break;
        }
    }

    [RelayCommand]
    private void Apply(ClockFaceItem? face)
    {
        if (face is null)
        {
            return;
        }
        Selected = face;
        Error = null;
        if (face.IsBuiltIn)
        {
            var index = ClockPresets.All.ToList().FindIndex(p => p.Id == face.PresetId);
            if (index >= 0)
            {
                _settings.PresetIndex = index;
            }
        }
        else
        {
            _settings.ApplyStyle(face.Style);
            _tools.SetSlots(face.Slots ?? _tools.Slots);
        }
        MarkApplied();
    }

    private bool HasSelection() => Selected is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ApplySelected() => Apply(Selected);

    // The selected face as text to keep or share (the same versioned format the database uses, for one face).
    public string Export(ClockFaceItem face) =>
        ClockFaceJson.SerializeOne(new CustomClockFace(face.Id ?? Guid.NewGuid(), face.Title, face.Style, face.Slots ?? _tools.Slots));

    // Adds a face from text made by Export; one that cannot be read (or does not fit) shows a message and changes nothing.
    public bool Import(string? text)
    {
        if (!ClockFaceJson.TryParseOne(text, out var face) || face is null)
        {
            Error = "That text is not a clock face.";
            return false;
        }
        var name = face.Name;
        for (var n = 1; _library.CheckName(name) is not null && n < 1000; n++)
        {
            name = n == 1 ? $"{face.Name} copy" : $"{face.Name} copy {n}";
        }
        var result = _library.SaveAsNew(name, face.Style, face.Slots);
        return Finish(result, result.Face?.Id);
    }
}
