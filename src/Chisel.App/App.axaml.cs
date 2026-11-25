using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chisel.App.Startup;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Services;
using Chisel.Core.Weather;
using Chisel.Infrastructure.Weather;

namespace Chisel.App;

public partial class App : Application
{
    private AppServices? _services;
    private Chisel.Core.Media.IMediaService? _mediaService;
    private Chisel.Core.Media.ISpectrumSource? _spectrum;
    private Chisel.Core.Media.IVolumeService? _volume;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The weather-motion capture never touches real data: a temp database, a fake weather service and an in-memory store.
            var scenario = Dev.CaptureMode.Scenario(desktop.Args);
            // The media capture plays sample metadata from a fake service; otherwise the real system media session is used.
            _mediaService = scenario is "media" or "media-visualizer" or "media-gradient" ? new FakeMediaService { Current = Dev.CaptureMode.SampleMedia() } : new WindowsMediaService();
            Dev.CaptureMode.CaptureMedia = _mediaService as FakeMediaService;
            // The visualizer capture feeds a synthetic signal; a normal run listens to the system output; other captures stay silent.
            _spectrum = scenario == "media-visualizer" ? new SyntheticSpectrumSource() : scenario is null ? new WasapiSpectrumSource() : null;
            // Captures never touch the real system volume.
            _volume = scenario is null ? new WindowsVolumeService() : new FakeVolumeService();
            var isolated = scenario is "weather-motion" or "media-visualizer" or "media-gradient";
            var databasePath = isolated ? Path.Combine(Path.GetTempPath(), $"chisel-capture-{Guid.NewGuid():N}.db") : AppPaths.GetDatabasePath();
            var startup = AppStartup.Run(() => new AppServices(databasePath), AppPaths.GetErrorLogPath());
            _services = startup.Value;

            if (_services is null)
            {
                desktop.MainWindow = new ErrorWindow(startup.ErrorMessage!);
            }
            else
            {
                var timeDisplay = new TimeDisplay();
                var paletteDisplay = new PaletteDisplay();
                // Settings first: it loads the saved clock format, palette and theme (applying the theme) before anything is shown.
                var theme = new Theming.ThemeService();
                var settings = new SettingsViewModel(_services.UserSettings, timeDisplay, paletteDisplay, theme: theme);
                IWeatherStore weatherStore = isolated ? new MemoryWeatherStore() : _services.Weather;
                var weather = isolated
                    ? new WeatherViewModel(Dev.CaptureMode.RainWeather(), _services.Time, null, timeDisplay)
                    : new WeatherViewModel(new OpenMeteoWeatherService(_services.Weather), _services.Time, _services.Weather, timeDisplay);
                MainWindow? mainWindow = null;
                var layout = new LayoutViewModel(_services.WidgetLayout, _services.Pictures, _services.LayoutProfiles);
                var alerts = new AlertsViewModel(new WindowsAlertSound());
                var mainViewModel = new MainViewModel(
                    new ClockViewModel(_services.Time, timeDisplay),
                    new ScheduleViewModel(_services.Schedule, _services.Markers, _services.Time, timeDisplay, paletteDisplay),
                    new TaskListViewModel(_services.Tasks, _services.Time),
                    new NotesViewModel(_services.Notes, _services.Time, _services.NotepadSettings, new NotepadFileService(() => mainWindow?.StorageProvider)),
                    new QuickLinksViewModel(_services.QuickLinks),
                    settings,
                    layout,
                    weather,
                    new WeatherSettingsViewModel(weatherStore, isolated ? new NoGeocoder() : new OpenMeteoGeocoder(), weather, userSettings: _services.UserSettings),
                    _mediaService,
                    _spectrum,
                    new MediaSettingsViewModel(_services.UserSettings),
                    _volume,
                    pictureCatalog: _services.Pictures,
                    clockSettings: new ClockSettingsViewModel(_services.ClockStyles),
                    clockFaces: _services.ClockFaces,
                    clockTools: new ClockToolsViewModel(_services.ClockTools, _services.Time),
                    alarms: new AlarmsViewModel(new Chisel.Core.Clock.AlarmService(_services.Alarms, _services.Time), alerts, timeDisplay),
                    alerts: alerts,
                    calculatorSettings: new CalculatorSettingsViewModel(_services.CalculatorSettings),
                    scripture: new ScriptureViewModel(_services.Time),
                    calendarSettings: new CalendarSettingsViewModel(_services.CalendarSettings),
                    dispatch: action => Avalonia.Threading.Dispatcher.UIThread.Post(action),
                    systemMonitorSettings: new SystemMonitorSettingsViewModel(_services.SystemMonitorSettings),
                    systemStats: new WindowsSystemStats(),
                    countdown: _services.Countdown,
                    focusTimer: _services.FocusTimer,
                    chime: new SoftChime(),
                    pictureImport: new PictureImportService(_services.PictureImporter, _services.PictureDownloader, _services.UserSettings, _services.PictureDropLog),
                    pictureSettings: new PictureSettingsViewModel(_services.UserSettings, layout, _services.Pictures, AppPaths.GetDropLogPath(),
                        folder => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true })));
                weather.ShowCached();
                mainViewModel.Refresh();

                mainWindow = new MainWindow(isolated ? null : AppPaths.GetWindowPlacementPath())
                {
                    DataContext = mainViewModel,
                };
                desktop.MainWindow = mainWindow;

                if (scenario is not null)
                {
                    Dev.CaptureMode.Run((MainWindow)desktop.MainWindow, scenario, () => desktop.Shutdown());
                }

                desktop.Exit += (_, _) =>
                {
                    mainViewModel.Notes.Flush();
                    mainViewModel.SystemMonitor.Dispose();
                    mainViewModel.Countdown.Dispose();
                    mainViewModel.FocusTimer.Dispose();
                    (_mediaService as IDisposable)?.Dispose();
                    _spectrum?.Dispose();
                    (_volume as IDisposable)?.Dispose();
                    _services?.Dispose();
                    if (isolated)
                    {
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                        try { File.Delete(databasePath); } catch (IOException) { }
                    }
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
