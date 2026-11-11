using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chisel.App.Startup;
using Chisel.App.ViewModels;
using Chisel.App.Views;

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
                var mainViewModel = new MainViewModel(
                    new ClockViewModel(_services.Time),
                    new ScheduleViewModel(_services.Schedule, _services.Markers, _services.Time),
                    new TaskListViewModel(_services.Tasks),
                    new NotesViewModel(_services.Notes, _services.Time),
                    new QuickLinksViewModel(_services.QuickLinks),
                    _services.Schedule,
                    _services.Markers);

                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel,
                };

                desktop.Exit += (_, _) => _services?.Dispose();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
