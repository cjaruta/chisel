using System;
using System.IO;

namespace Chisel.App.Startup;

public static class AppPaths
{
    public static string GetDatabasePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chisel", "chisel.db");

    public static string GetErrorLogPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chisel", "error.log");

    public static string GetWindowPlacementPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chisel", "window.json");
}
