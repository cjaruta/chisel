using Avalonia;
using System;
using Chisel.App.Startup;
using Chisel.Infrastructure.Startup;

namespace Chisel.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    // Catches what nothing else did. The app adds the UI thread and fills in how to save and what to show once it is up.
    public static CrashHandler Crashes { get; } =
        new(new CrashLog(AppPaths.GetLogsFolder(), typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"));

    [STAThread]
    public static void Main(string[] args)
    {
        Crashes.InstallProcessHandlers();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
