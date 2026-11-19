using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chisel.App.Startup;
using Chisel.App.ViewModels;
using Chisel.App.Views;
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
            var startup = AppStartup.Run(() => new AppServices(AppPaths.GetDatabasePath()), AppPaths.GetErrorLogPath());
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
                var weather = new WeatherViewModel(new OpenMeteoWeatherService(_services.Weather), _services.Time, _services.Weather);
                var mainViewModel = new MainViewModel(
                    new ClockViewModel(_services.Time, timeDisplay),
                    new ScheduleViewModel(_services.Schedule, _services.Markers, _services.Time, timeDisplay, paletteDisplay),
                    new TaskListViewModel(_services.Tasks),
                    new NotesViewModel(_services.Notes, _services.Time, timeDisplay),
                    new QuickLinksViewModel(_services.QuickLinks),
                    settings,
                    new LayoutViewModel(_services.WidgetLayout),
                    weather,
                    new WeatherSettingsViewModel(_services.Weather, new OpenMeteoGeocoder(), weather, userSettings: _services.UserSettings));
                weather.ShowCached();
                mainViewModel.Refresh();

                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel,
                };

                if (Dev.CaptureMode.Scenario(desktop.Args) is { } scenario)
                {
                    Dev.CaptureMode.Run((MainWindow)desktop.MainWindow, scenario, () => desktop.Shutdown());
                }

                desktop.Exit +=(_, _) => _services?.Dispose();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
