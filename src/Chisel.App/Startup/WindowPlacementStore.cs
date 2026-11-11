using System;
using System.IO;
using System.Text.Json;

namespace Chisel.App.Startup;

public static class WindowPlacementStore
{
    public static WindowPlacement? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<WindowPlacement>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Save(string path, WindowPlacement placement)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(placement));
    }
}
