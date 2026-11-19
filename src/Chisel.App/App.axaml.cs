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
            _mediaService = scenario is "media" or "media-visualizer" ? new FakeMediaService { Current = Dev.CaptureMode.SampleMedia() } : new WindowsMediaService();
            // The visualizer capture feeds a synthetic signal; a normal run listens to the system output; other captures stay silent.
            _spectrum = scenario == "media-visualizer" ? new SyntheticSpectrumSource() : scenario is null ? new WasapiSpectrumSource() : null;
            // Captures never touch the real system volume.
            _volume = scenario is null ? new WindowsVolumeService() : new FakeVolumeService();
            var isolated = scenario is "weather-motion" or "media-visualizer";
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
                var mainViewModel = new MainViewModel(
                    new ClockViewModel(_services.Time, timeDisplay),
                    new ScheduleViewModel(_services.Schedule, _services.Markers, _services.Time, timeDisplay, paletteDisplay),
                    new TaskListViewModel(_services.Tasks),
                    new NotesViewModel(_services.Notes, _services.Time, timeDisplay),
                    new QuickLinksViewModel(_services.QuickLinks),
                    settings,
                    new LayoutViewModel(_services.WidgetLayout),
                    weather,
                    new WeatherSettingsViewModel(weatherStore, isolated ? new NoGeocoder() : new OpenMeteoGeocoder(), weather, userSettings: _services.UserSettings),
                    _mediaService,
                    _spectrum,
                    new MediaSettingsViewModel(_services.UserSettings),
                    _volume);
                weather.ShowCached();
                mainViewModel.Refresh();

                desktop.MainWindow = new MainWindow(isolated ? null : AppPaths.GetWindowPlacementPath())
                {
                    DataContext = mainViewModel,
                };

                if (scenario is not null)
                {
                    Dev.CaptureMode.Run((MainWindow)desktop.MainWindow, scenario, () => desktop.Shutdown());
                }

                desktop.Exit += (_, _) =>
                {
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
