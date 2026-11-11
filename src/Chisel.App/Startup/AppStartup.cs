using System;
using Chisel.Infrastructure.Startup;

namespace Chisel.App.Startup;

public sealed record StartupResult<T>(T? Value, string? ErrorMessage) where T : class;

public static class AppStartup
{
    public static StartupResult<T> Run<T>(Func<T> create, string logPath) where T : class
    {
        try
        {
            return new StartupResult<T>(create(), null);
        }
        catch (Exception ex)
        {
            try
            {
                ErrorLogger.Log(logPath, ex);
                return new StartupResult<T>(null,
                    $"Chisel couldn't open or update its database. Details were written to:\n{logPath}");
            }
            catch (Exception)
            {
                return new StartupResult<T>(null,
                    $"Chisel couldn't open or update its database, and the log file couldn't be written either.\n\n{ex.Message}");
            }
        }
    }
}
