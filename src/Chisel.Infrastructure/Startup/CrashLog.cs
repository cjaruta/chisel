using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Chisel.Infrastructure.Files;

namespace Chisel.Infrastructure.Startup;

// Writes a short crash report to logs\crash-<date-time>.log: time, Chisel and Windows version, where it happened, and each
// exception's type and stack trace. Exception messages are never written (they can quote what the user typed), the user's
// profile folder becomes %USERPROFILE%, and source paths are cut to the file name. Only the newest few reports are kept,
// each capped in size. Writing never throws: a crash report must not cause another crash.
public sealed partial class CrashLog
{
    public const int DefaultKeep = 5;
    public const int DefaultMaxBytes = 64 * 1024;

    private readonly string _appVersion;
    private readonly TimeProvider _time;
    private readonly string? _profilePath;
    private readonly string _os;
    private readonly int _keep;
    private readonly int _maxBytes;

    public CrashLog(string folder, string appVersion, TimeProvider? time = null, string? profilePath = null, string? os = null,
        int keep = DefaultKeep, int maxBytes = DefaultMaxBytes)
    {
        Folder = folder;
        _appVersion = appVersion;
        _time = time ?? TimeProvider.System;
        _profilePath = profilePath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _os = os ?? Environment.OSVersion.VersionString;
        _keep = Math.Max(1, keep);
        _maxBytes = Math.Max(1024, maxBytes);
    }

    public string Folder { get; }

    // The report's path, or null when it couldn't be written.
    public string? Write(Exception exception, string source)
    {
        try
        {
            var now = _time.GetUtcNow();
            var text = Cap(Format(exception, source, _appVersion, _os, now, _profilePath), _maxBytes);
            var path = Path.Combine(Folder, $"crash-{now:yyyyMMdd-HHmmss}.log");
            for (var n = 2; File.Exists(path); n++)
            {
                path = Path.Combine(Folder, $"crash-{now:yyyyMMdd-HHmmss}-{n}.log");
            }
            AtomicFile.WriteAllText(path, text);
            foreach (var old in Directory.GetFiles(Folder, "crash-*.log").OrderByDescending(f => f, StringComparer.Ordinal).Skip(_keep))
            {
                File.Delete(old);
            }
            return path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string Format(Exception exception, string source, string appVersion, string os, DateTimeOffset now, string? profilePath)
    {
        var text = new StringBuilder()
            .AppendLine($"Time: {now.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC")
            .AppendLine($"Version: Chisel {appVersion}")
            .AppendLine($"OS: {os}")
            .AppendLine($"Where: {source}");
        var label = "Exception";
        for (Exception? e = exception; e is not null; e = e.InnerException, label = "Caused by")
        {
            text.AppendLine().AppendLine($"{label}: {e.GetType().FullName}");
            if (e.StackTrace is { Length: > 0 } stack)
            {
                text.AppendLine(stack.TrimEnd());
            }
        }
        return Redact(text.ToString(), profilePath);
    }

    public static string Redact(string text, string? profilePath)
    {
        text = SourcePath().Replace(text, " in $1:line $2");
        if (!string.IsNullOrEmpty(profilePath))
        {
            text = text.Replace(profilePath.TrimEnd('\\', '/'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }
        return AnyProfile().Replace(text, "%USERPROFILE%");
    }

    private static string Cap(string text, int maxBytes)
    {
        const string Cut = "\n[cut to fit the size limit]\n";
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }
        var length = Math.Min(text.Length, maxBytes - Cut.Length);
        while (length > 0 && Encoding.UTF8.GetByteCount(text.AsSpan(0, length)) > maxBytes - Cut.Length)
        {
            length -= 256;
        }
        return text[..Math.Max(0, length)] + Cut;
    }

    // " in C:\any\folder\File.cs:line 12" -> " in File.cs:line 12"
    [GeneratedRegex(@" in (?:[^\r\n]*[\\/])?([^\\/\r\n]+?):line (\d+)")]
    private static partial Regex SourcePath();

    // Any other user's profile folder, such as C:\Users\Someone.
    [GeneratedRegex(@"[A-Za-z]:[\\/]Users[\\/][^\\/\r\n)]+", RegexOptions.IgnoreCase)]
    private static partial Regex AnyProfile();
}
