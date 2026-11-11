using System;
using System.IO;

namespace Chisel.Infrastructure.Startup;

public static class ErrorLogger
{
    public static void Log(string logPath, Exception exception, TimeProvider? time = null)
    {
        var timestamp = (time ?? TimeProvider.System).GetUtcNow();
        var entry = $"[{timestamp:yyyy-MM-dd HH:mm:ss} UTC] {exception}{Environment.NewLine}";

        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(logPath, entry);
    }
}
