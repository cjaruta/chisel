using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using Chisel.App.Startup;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Services;
using Chisel.Core.Weather;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Weather;

namespace Chisel.App;

public partial class App : Application
{
    private AppServices? _services;
    private Chisel.Core.Media.IMediaService? _mediaService;
    private Chisel.Core.Media.ISpectrumSource? _spectrum;
    private Chisel.Core.Media.IVolumeService? _volume;
    private TrayIcon? _tray;

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
                var theme = new Theming.ThemeService(new Theming.AvaloniaPlatformTheme(PlatformSettings));
                // Start with Windows: the Run entry follows Chisel if it was moved (only when it is already on). Captures never touch it.
                StartWithWindows? startWithWindows = null;
                if (scenario is null)
                {
                    startWithWindows = new StartWithWindows(new WindowsStartupRegistry(), ProgramPath());
                    startWithWindows.Revalidate();
                }
                var system = new SystemSettingsViewModel(_services.SystemSettings, startWithWindows);
                var settings = new SettingsViewModel(_services.UserSettings, timeDisplay, paletteDisplay, theme: theme, system: system);
                IWeatherStore weatherStore = isolated ? new MemoryWeatherStore() : _services.Weather;
                var weather = isolated
                    ? new WeatherViewModel(Dev.CaptureMode.RainWeather(), _services.Time, null, timeDisplay)
                    : new WeatherViewModel(new OpenMeteoWeatherService(_services.Weather), _services.Time, _services.Weather, timeDisplay);
                MainWindow? mainWindow = null;
                var layout = new LayoutViewModel(_services.WidgetLayout, _services.Pictures, _services.LayoutProfiles);
                var alerts = new AlertsViewModel(new WindowsAlertSound());
                var clipboardLog = new DebugClipboardLog();
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
                    habits: _services.Habits,
                    converter: _services.ConverterSettings,
                    clipboardHistory: new ClipboardHistoryViewModel(
                        new WindowsClipboardService(() => mainWindow?.Clipboard, clipboardLog),
                        _services.ClipboardSettings, new ClipboardHistoryFileStore(AppPaths.GetClipboardHistoryPath()), log: clipboardLog),
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

                // The tray icon, closing to it and the show/hide shortcut. Captures run without them.
                AppShell? shell = null;
                _tray = scenario is null ? CreateTray(() => shell, settings) : null;
                shell = new AppShell(new WindowShell(mainWindow, desktop), () => system.Current, _tray is not null, () => desktop.Shutdown());
                // The app ends when the main window really closes, not when it hides to the tray (or never showed).
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                mainWindow.Closing += (_, e) =>
                {
                    if (shell.HandleClosing(userClose: e.CloseReason == WindowCloseReason.WindowClosing && !e.IsProgrammatic))
                    {
                        e.Cancel = true;
                    }
                };
                mainWindow.Closed += (_, _) =>
                {
                    if (!shell.IsQuitting)
                    {
                        shell.Quit();
                    }
                };
                mainWindow.Deactivated += (_, _) => shell.NoteDeactivated();

                // The show/hide shortcut (RegisterHotKey on the main window; no keyboard hooks).
                var hotkeys = scenario is null ? new Win32HotkeyService(mainWindow) : null;
                if (hotkeys is not null)
                {
                    hotkeys.Pressed += shell.Toggle;
                    system.AttachHotkeys(hotkeys, message => alerts.Show("Shortcut not available", message, sound: false));
                }
                AppDomain.CurrentDomain.UnhandledException += (_, _) => DisposeTray();

                // Started with Windows and set to wait in the tray: the window is made but not shown until asked for.
                if (!shell.ShouldStartHidden(StartupArguments.Parse(desktop.Args).Minimized))
                {
                    desktop.MainWindow = mainWindow;
                }

                if (scenario is not null)
                {
                    Dev.CaptureMode.Run(mainWindow, scenario, () => desktop.Shutdown());
                }

                desktop.Exit += (_, _) =>
                {
                    DisposeTray();
                    system.ReleaseHotkey();
                    hotkeys?.Dispose();
                    mainViewModel.Notes.Flush();
                    mainViewModel.SystemMonitor.Dispose();
                    mainViewModel.Countdown.Dispose();
                    mainViewModel.FocusTimer.Dispose();
                    mainViewModel.Habits.Dispose();
                    mainViewModel.ClipboardHistory.Dispose();
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

    // The running Chisel.App.exe, or null when started some other way (through dotnet.exe), which can't be put in the Run key.
    private static string? ProgramPath()
    {
        var path = Environment.ProcessPath;
        return path is not null && !Path.GetFileName(path).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    // The tray icon with the app icon and its menu; null when Windows would not give us one (the window then never hides).
    private TrayIcon? CreateTray(Func<AppShell?> shell, SettingsViewModel settings)
    {
        try
        {
            using var iconStream = AssetLoader.Open(new Uri("avares://Chisel.App/Assets/chiselIcon.ico"));
            var showHide = new NativeMenuItem("Show/Hide");
            showHide.Click += (_, _) => shell()?.MenuShowHide();
            var openSettings = new NativeMenuItem("Settings");
            openSettings.Click += (_, _) =>
            {
                shell()?.Show();
                settings.SelectedTab = SettingsTab.General;
                settings.IsOpen = true;
            };
            var quit = new NativeMenuItem("Quit");
            quit.Click += (_, _) => shell()?.Quit();
            var tray = new TrayIcon
            {
                Icon = new WindowIcon(iconStream),
                ToolTipText = "Chisel",
                Menu = new NativeMenu { showHide, openSettings, new NativeMenuItemSeparator(), quit },
                IsVisible = true,
            };
            tray.Clicked += (_, _) => shell()?.TrayClicked();
            TrayIcon.SetIcons(this, new TrayIcons { tray });
            return tray;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Removes the tray icon right away so no ghost icon is left behind after exit.
    private void DisposeTray()
    {
        var tray = _tray;
        _tray = null;
        if (tray is null)
        {
            return;
        }
        tray.IsVisible = false;
        tray.Dispose();
    }
}
