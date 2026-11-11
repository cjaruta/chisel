namespace Chisel.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ClockViewModel Clock { get; }
    public ScheduleViewModel Schedule { get; }
    public TaskListViewModel Tasks { get; }
    public NotesViewModel Notes { get; }
    public QuickLinksViewModel QuickLinks { get; }

    public MainViewModel(
        ClockViewModel clock,
        ScheduleViewModel schedule,
        TaskListViewModel tasks,
        NotesViewModel notes,
        QuickLinksViewModel quickLinks)
    {
        Clock = clock;
        Schedule = schedule;
        Tasks = tasks;
        Notes = notes;
        QuickLinks = quickLinks;
    }

    public void Refresh()
    {
        Clock.Refresh();
        Schedule.Refresh();
    }
}
