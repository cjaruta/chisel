using System.Security.Cryptography;
using System.Text;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Files;
using Microsoft.EntityFrameworkCore;

namespace Chisel.Infrastructure.Repositories;

// Saved widget data that can't be read is copied to "damaged\<name>-<hash>.bad" next to the database before the widget
// starts empty, so its next save never silently throws the only copy away. The same text is kept once; an in-memory
// database has nowhere to keep it, and a failure to keep it never breaks the widget.
internal static class DamagedData
{
    public static void Keep(ChiselDbContext context, string name, string text)
    {
        try
        {
            var source = context.Database.GetDbConnection().DataSource;
            if (string.IsNullOrEmpty(source) || source == ":memory:" || Path.GetDirectoryName(Path.GetFullPath(source)) is not { } folder)
            {
                return;
            }
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();
            var path = Path.Combine(folder, "damaged", $"{name}-{hash}.bad");
            if (!File.Exists(path))
            {
                AtomicFile.WriteAllText(path, text);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Keeping the copy is best effort.
        }
    }
}
