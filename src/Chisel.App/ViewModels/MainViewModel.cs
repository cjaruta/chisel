using System;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ClockViewModel Clock { get; }
    public AnalogClockViewModel AnalogClock { get; }
    public WeatherViewModel Weather { get; }
    public WeatherSettingsViewModel WeatherSettings { get; }
    public ClockSettingsViewModel ClockSettings { get; }
    public ClockFacesViewModel ClockFaces { get; }
    public ClockToolsViewModel ClockTools { get; }
    public CalculatorSettingsViewModel CalculatorSettings { get; }
    public CalculatorViewModel Calculator { get; }
    public CalendarSettingsViewModel CalendarSettings { get; }
    public CalendarViewModel Calendar { get; }
    public SystemMonitorSettingsViewModel SystemMonitorSettings { get; }
    public SystemMonitorViewModel SystemMonitor { get; }
    public AlertsViewModel Alerts { get; }
    public AlarmsViewModel Alarms { get; }
    public ClockSlotsViewModel ClockSlots { get; }
    public ClockSlotsSettingsViewModel ClockSlotsSettings { get; }
    public MediaViewModel Media { get; }
    public MediaBackgroundViewModel MediaBackground { get; }
    public MediaSettingsViewModel MediaSettings { get; }
    public VolumeViewModel Volume { get; }
    // Where the media widget's audio bars come from; null means flat bars.
    public Chisel.Core.Media.ISpectrumSource? Spectrum { get; }
    public ScheduleViewModel Schedule { get; }
    public TaskListViewModel Tasks { get; }
    public NotesViewModel Notes { get; }
    public QuickLinksViewModel QuickLinks { get; }
    public SettingsViewModel Settings { get; }
    public PictureFramesViewModel PictureFrames { get; }
    public PictureSettingsViewModel PictureSettings { get; }
    public ScriptureViewModel Scripture { get; }

    // Which widgets are shown where; the window's layout host is built from it.
    public LayoutViewModel Layout { get; }

    // The edit-mode widget library (search, add, see what is on the dashboard).
    public WidgetLibraryViewModel Library { get; }

    public MainViewModel(
        ClockViewModel clock,
        ScheduleViewModel schedule,
        TaskListViewModel tasks,
        NotesViewModel notes,
        QuickLinksViewModel quickLinks,
        SettingsViewModel settings,
        LayoutViewModel? layout = null,
        WeatherViewModel? weather = null,
        WeatherSettingsViewModel? weatherSettings = null,
        Chisel.Core.Media.IMediaService? media = null,
        Chisel.Core.Media.ISpectrumSource? spectrum = null,
        MediaSettingsViewModel? mediaSettings = null,
        Chisel.Core.Media.IVolumeService? volume = null,
        Services.IArtSampler? artSampler = null,
        Services.PictureImportService? pictureImport = null,
        PictureSettingsViewModel? pictureSettings = null,
        TimeProvider? time = null,
        Chisel.Core.Pictures.PictureCatalog? pictureCatalog = null,
        ClockSettingsViewModel? clockSettings = null,
        ClockToolsViewModel? clockTools = null,
        AlarmsViewModel? alarms = null,
        AlertsViewModel? alerts = null,
        CalculatorSettingsViewModel? calculatorSettings = null,
        ScriptureViewModel? scripture = null,
        Chisel.Core.Clock.IClockFaceStore? clockFaces = null,
        CalendarSettingsViewModel? calendarSettings = null,
        Action<Action>? dispatch = null,
        SystemMonitorSettingsViewModel? systemMonitorSettings = null,
        Chisel.Core.SystemMonitor.ISystemStatsSource? systemStats = null)
    {
        Clock = clock;
        AnalogClock = new AnalogClockViewModel(clock);
        Weather = weather ?? new WeatherViewModel(new Services.FakeWeatherService(), TimeProvider.System);
        WeatherSettings = weatherSettings ?? new WeatherSettingsViewModel(new Services.MemoryWeatherStore(), new Services.NoGeocoder(), Weather);
        ClockSettings = clockSettings ?? new ClockSettingsViewModel();
        ClockTools = clockTools ?? new ClockToolsViewModel(null, time ?? TimeProvider.System);
        ClockFaces = new ClockFacesViewModel(clockFaces, ClockSettings, ClockTools);
        CalculatorSettings = calculatorSettings ?? new CalculatorSettingsViewModel(null);
        Calculator = new CalculatorViewModel(CalculatorSettings);
        CalendarSettings = calendarSettings ?? new CalendarSettingsViewModel(null);
        Calendar = new CalendarViewModel(CalendarSettings, time ?? TimeProvider.System, dispatch: dispatch);
        SystemMonitorSettings = systemMonitorSettings ?? new SystemMonitorSettingsViewModel(null);
        SystemMonitor = new SystemMonitorViewModel(SystemMonitorSettings, systemStats ?? new Services.NoSystemStats(), time ?? TimeProvider.System, dispatch);
        Alerts = alerts ?? new AlertsViewModel();
        Scripture = scripture ?? new ScriptureViewModel(time ?? TimeProvider.System);
        Alarms = alarms ?? new AlarmsViewModel(new Chisel.Core.Clock.AlarmService(new Services.MemoryAlarmRepository(), time ?? TimeProvider.System), Alerts);
        ClockTools.TimerFinished += () => Alerts.Show("Timer", "Time's up");
        ClockSlots = new ClockSlotsViewModel(ClockTools, Alarms);
        ClockSlotsSettings = new ClockSlotsSettingsViewModel(ClockTools);
        Spectrum = spectrum;
        MediaSettings = mediaSettings ?? new MediaSettingsViewModel(null);
        Volume = new VolumeViewModel(volume ?? new Services.FakeVolumeService(), TimeProvider.System);
        Media = new MediaViewModel(media ?? new Services.FakeMediaService(), TimeProvider.System);
        MediaBackground = new MediaBackgroundViewModel(Media, MediaSettings, settings.Theme, artSampler ?? new Services.AvaloniaArtSampler());
        Schedule = schedule;
        Tasks = tasks;
        Notes = notes;
        QuickLinks = quickLinks;
        Settings = settings;
        Layout = layout ?? new LayoutViewModel();
        Library = new WidgetLibraryViewModel(Layout);
        PictureFrames = new PictureFramesViewModel(Layout, time, import: pictureImport, catalog: pictureCatalog);
        PictureSettings = pictureSettings ?? new PictureSettingsViewModel(null, Layout, null);
        Layout.AdjustGate = id => PictureFrames.For(id).CanAdjust;
        Volume.ScrollEnabled = MediaSettings.ScrollVolume;
        MediaSettings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MediaSettingsViewModel.ScrollVolume))
            {
                Volume.ScrollEnabled = MediaSettings.ScrollVolume;
            }
        };
        Layout.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LayoutViewModel.IsEditing))
            {
                Volume.ScrollBlocked = Layout.IsEditing;
                OnPropertyChanged(nameof(CanOpenSettings));
            }
            else if (e.PropertyName == nameof(LayoutViewModel.Layout))
            {
                UpdateMediaActive();
                // A notepad taken off the dashboard keeps what was just typed.
                if (!System.Linq.Enumerable.Contains(Layout.Layout.WidgetIds, WidgetRegistry.Notes))
                {
                    Notes.Flush();
                }
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

    // Called often (about every 200 ms); refreshes only when the local second has changed, so a second is
    // neither skipped nor doubled.
    public void Tick()
    {
        Media.Tick();
        Notes.Tick();
        PictureFrames.Tick();
        ClockTools.Tick();
        Alarms.Tick();
        Scripture.Tick();
        ClockSlots.Refresh();
        if (Clock.NeedsRefresh)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        Clock.Refresh();
        Schedule.UpdateNow();
        // The weather is only fetched while its widget is on the dashboard.
        Weather.IsActive = System.Linq.Enumerable.Contains(Layout.Layout.WidgetIds, WidgetRegistry.Weather);
        Weather.Refresh();
        UpdateMediaActive();
    }

    // The media widget only listens to the system while it is on the dashboard.
    private void UpdateMediaActive() =>
        Media.IsActive = System.Linq.Enumerable.Contains(Layout.Layout.WidgetIds, WidgetRegistry.Media);

    // Escape cancels a widget drag or resize; otherwise it closes the widget library; otherwise it cancels layout editing; otherwise it closes whatever
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
        // The library closes first; edit mode stays.
        if (Library.IsOpen)
        {
            Library.CloseCommand.Execute(null);
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
