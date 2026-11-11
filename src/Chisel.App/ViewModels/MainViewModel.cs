using System;
using Chisel.Core.Schedule;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IScheduleRepository _scheduleRepository;
    private readonly IMarkerRepository _markerRepository;

    public ClockViewModel Clock { get; }
    public ScheduleViewModel Schedule { get; }
    public TaskListViewModel Tasks { get; }
    public NotesViewModel Notes { get; }
    public QuickLinksViewModel QuickLinks { get; }
    public SettingsViewModel Settings { get; }

    public event Action<ScheduleEditorViewModel>? ScheduleEditorRequested;

    public MainViewModel(
        ClockViewModel clock,
        ScheduleViewModel schedule,
        TaskListViewModel tasks,
        NotesViewModel notes,
        QuickLinksViewModel quickLinks,
        IScheduleRepository scheduleRepository,
        IMarkerRepository markerRepository,
        SettingsViewModel settings)
    {
        Clock = clock;
        Schedule = schedule;
        Tasks = tasks;
        Notes = notes;
        QuickLinks = quickLinks;
        Settings = settings;
        _scheduleRepository = scheduleRepository;
        _markerRepository = markerRepository;
    }

    public void Refresh()
    {
        Clock.Refresh();
        Schedule.Refresh();
    }

    [RelayCommand]
    private void OpenScheduleEditor()
    {
        var editor = new ScheduleEditorViewModel(_scheduleRepository, _markerRepository);
        editor.Saved += Schedule.Refresh;
        ScheduleEditorRequested?.Invoke(editor);
    }
}
