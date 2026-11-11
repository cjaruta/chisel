using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chisel.App.Startup;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.Infrastructure.Startup;

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
            try
            {
                _services = new AppServices(AppPaths.GetDatabasePath());

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
            catch (Exception ex)
            {
                var logPath = AppPaths.GetErrorLogPath();
                ErrorLogger.Log(logPath, ex);

                desktop.MainWindow = new ErrorWindow(
                    $"Something went wrong starting the database. Details were written to:\n{logPath}");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
