using System;
using Chisel.Infrastructure.Startup;

namespace Chisel.App.Startup;

public sealed record StartupResult<T>(T? Value, string? ErrorMessage) where T : class;

public static class AppStartup
{
    public static StartupResult<T> Run<T>(Func<T> create, CrashLog log) where T : class
    {
        try
        {
            return new StartupResult<T>(create(), null);
        }
        catch (Exception ex)
        {
            return log.Write(ex, "startup") is { } path
                ? new StartupResult<T>(null, $"Chisel couldn't open or update its database. A short report was written to:\n{path}")
                : new StartupResult<T>(null,
                    $"Chisel couldn't open or update its database, and the report couldn't be written either.\n\n{ex.Message}");
        }
    }
}
