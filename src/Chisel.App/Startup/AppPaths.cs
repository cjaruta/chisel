using System;
using System.IO;

namespace Chisel.App.Startup;

public static class AppPaths
{
    public static string GetDatabasePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chisel", "chisel.db");

    // Picture frame files: pictures\<content hash>.<extension>, next to the database.
    public static string GetPicturesFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chisel", "pictures");

    // The picture drop diagnostics: %AppData%\Chisel\logs\picture-drop.log
    public static string GetDropLogPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chisel", "logs", "picture-drop.log");

    public static string GetErrorLogPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chisel", "error.log");

    public static string GetWindowPlacementPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chisel", "window.json");
}
