using System;
using System.IO;

namespace Chisel.App.Startup;

public static class AppPaths
{
    public static string GetDatabasePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chisel", "chisel.db");
}
