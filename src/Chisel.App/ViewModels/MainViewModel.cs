using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ClockViewModel Clock { get; }
    public AnalogClockViewModel AnalogClock { get; }
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
        AnalogClock = new AnalogClockViewModel(clock);
        Schedule = schedule;
        Tasks = tasks;
        Notes = notes;
        QuickLinks = quickLinks;
        Settings = settings;
        Layout = layout ?? new LayoutViewModel();
        Layout.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LayoutViewModel.IsEditing))
            {
                OnPropertyChanged(nameof(CanOpenSettings));
            }
        };
    }

    // The settings gear is off while the layout is being edited.
    public bool CanOpenSettings => !Layout.IsEditing;

    // Enters layout edit mode, closing anything that floats over the widgets first.
    [RelayCommand]
    private void CustomizeLayout()
    {
        if (Layout.IsEditing)
        {
            return;
        }
        Settings.IsOpen = false;
        Schedule.CancelDrag();
        Schedule.CloseEditorsCommand.Execute(null);
        Schedule.HideHover();
        Layout.EnterEditCommand.Execute(null);
    }

    public void Refresh()
    {
        Clock.Refresh();
        Schedule.UpdateNow();
    }

    // Escape cancels a widget drag or resize; otherwise it cancels layout editing; otherwise it closes whatever
    // floats over the window: settings or a schedule editor.
    [RelayCommand]
    private void CloseOverlays()
    {
        if (Layout.IsDraggingWidget)
        {
            Layout.CancelWidgetDrag();
            return;
        }
        if (Layout.IsResizing)
        {
            Layout.CancelResize();
            return;
        }
        if (Layout.IsEditing)
        {
            Layout.CancelCommand.Execute(null);
            return;
        }
        Settings.IsOpen = false;
        Schedule.CloseEditorsCommand.Execute(null);
    }
}
