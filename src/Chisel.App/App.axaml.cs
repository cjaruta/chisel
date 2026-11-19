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
            var isolated = scenario == "weather-motion";
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
                // Settings first: it loads the saved clock format and palette before anything is shown.
                var settings = new SettingsViewModel(_services.UserSettings, timeDisplay, paletteDisplay);
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
                    new WeatherSettingsViewModel(weatherStore, isolated ? new NoGeocoder() : new OpenMeteoGeocoder(), weather, userSettings: _services.UserSettings));
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
