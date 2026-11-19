namespace Chisel.Core.Media;

public static class MediaFormat
{
    // 3:05, or 1:02:05 from an hour up.
    public static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }
        var seconds = (int)time.TotalSeconds;
        return seconds >= 3600
            ? $"{seconds / 3600}:{seconds % 3600 / 60:00}:{seconds % 60:00}"
            : $"{seconds / 60}:{seconds % 60:00}";
    }

    public static double Progress(TimeSpan position, TimeSpan duration) =>
        duration <= TimeSpan.Zero ? 0 : Math.Clamp(position / duration, 0, 1);

    // A readable app name from the id Windows gives: "Spotify.exe" -> "Spotify",
    // "Pkg_8wekyb3d8bbwe!Publisher.App" -> "App".
    public static string SourceName(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return "";
        }
        var name = appId.Trim();
        var bang = name.LastIndexOf('!');
        if (bang >= 0)
        {
            name = name[(bang + 1)..];
        }
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }
        var dot = name.LastIndexOf('.');
        if (dot >= 0 && dot < name.Length - 1)
        {
            name = name[(dot + 1)..];
        }
        return name.Length == 0 ? "" : char.ToUpperInvariant(name[0]) + name[1..];
    }
}

public readonly record struct MediaLayoutPlan(double ArtSize, bool ShowAlbum, bool ShowSource, bool VolumeBesideButtons);

// What fits in the card body (width and height in pixels, padding already removed).
public static class MediaLayout
{
    public static MediaLayoutPlan For(double width, double height) =>
        new(width < 200 ? 0 : Math.Min(height, 96), height >= 130, height >= 150, height < 110);
}
