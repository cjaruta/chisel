using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ClockViewModel Clock { get; }
    public ScheduleViewModel Schedule { get; }
    public TaskListViewModel Tasks { get; }
    public NotesViewModel Notes { get; }
    public QuickLinksViewModel QuickLinks { get; }
    public SettingsViewModel Settings { get; }

    // Which widgets are shown where; the window's layout host is built from it.
    public LayoutViewModel Layout { get; }

    public MainViewModel(
        ClockViewModel clock,
        ScheduleViewModel schedule,
        TaskListViewModel tasks,
        NotesViewModel notes,
        QuickLinksViewModel quickLinks,
        SettingsViewModel settings,
        LayoutViewModel? layout = null)
    {
        Clock = clock;
        Schedule = schedule;
        Tasks = tasks;
        Notes = notes;
        QuickLinks = quickLinks;
        Settings = settings;
        Layout = layout ?? new LayoutViewModel();
    }

    public void Refresh()
    {
        Clock.Refresh();
        Schedule.UpdateNow();
    }

    // Escape closes whatever floats over the window: settings or a schedule editor.
    [RelayCommand]
    private void CloseOverlays()
    {
        Settings.IsOpen = false;
        Schedule.CloseEditorsCommand.Execute(null);
    }
}
