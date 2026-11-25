using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Chisel.Infrastructure.Startup;

// KeptAs: the file name the damaged database was kept under, or null when it opened normally.
public sealed record RecoveredOpen<T>(T Value, string? KeptAs);

// Opens the database, and when the file is damaged (not a database, truncated, corrupt) keeps it as
// "chisel.db.<date-time>.bad" next to it and starts with a fresh one, so a bad file never stops Chisel from starting
// and nothing is deleted. Other errors (the file is locked, no permission) are passed on untouched.
public static class DatabaseRecovery
{
    private const int SqliteCorrupt = 11;
    private const int SqliteNotADatabase = 26;

    public static RecoveredOpen<T> Open<T>(string dbPath, Func<T> open, TimeProvider? time = null)
    {
        if (IsDamaged(dbPath))
        {
            return new RecoveredOpen<T>(OpenFresh(dbPath, open, time), KeptName(dbPath, time));
        }
        try
        {
            return new RecoveredOpen<T>(open(), null);
        }
        catch (Exception ex) when (IsDamage(ex))
        {
            return new RecoveredOpen<T>(OpenFresh(dbPath, open, time), KeptName(dbPath, time));
        }
    }

    // A quick structural check before EF touches the file; an empty or missing file is simply a new database.
    private static bool IsDamaged(string dbPath)
    {
        if (!File.Exists(dbPath) || new FileInfo(dbPath).Length == 0)
        {
            return false;
        }
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            return command.ExecuteScalar() as string != "ok";
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is SqliteCorrupt or SqliteNotADatabase)
        {
            return true;
        }
    }

    private static bool IsDamage(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is SqliteException { SqliteErrorCode: SqliteCorrupt or SqliteNotADatabase })
            {
                return true;
            }
        }
        return false;
    }

    private static string KeptName(string dbPath, TimeProvider? time) =>
        $"{Path.GetFileName(dbPath)}.{(time ?? TimeProvider.System).GetLocalNow():yyyy-MM-dd-HHmmss}.bad";

    private static T OpenFresh<T>(string dbPath, Func<T> open, TimeProvider? time)
    {
        SqliteConnection.ClearAllPools();
        var kept = Path.Combine(Path.GetDirectoryName(dbPath) ?? "", KeptName(dbPath, time));
        File.Move(dbPath, kept);
        // The journal files belong to the damaged database; they are kept beside it rather than applied to the new one.
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            if (File.Exists(dbPath + suffix))
            {
                File.Move(dbPath + suffix, kept + suffix);
            }
        }
        return open();
    }
}
