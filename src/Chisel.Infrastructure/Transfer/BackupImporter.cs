using System.Collections;
using Chisel.Core.Countdown;
using Chisel.Core.Habits;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;

namespace Chisel.Infrastructure.Transfer;

// Applies a checked backup in one database transaction: either all of it lands or none of it does.
public static class BackupImporter
{
    public static void Apply(ChiselDbContext context, BackupPackage package, ImportMode mode)
    {
        using var transaction = context.Database.BeginTransaction();
        try
        {
            if (mode == ImportMode.Replace)
            {
                Replace(context, package);
            }
            else
            {
                Merge(context, package);
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            // Nothing half-applied stays behind in memory either; the next read comes from the database.
            context.ChangeTracker.Clear();
        }
    }

    private static void Replace(ChiselDbContext context, BackupPackage package)
    {
        foreach (var table in BackupTables.All)
        {
            table.RemoveAll(context);
        }
        // The weather snapshot belongs to the old place.
        context.WeatherCaches.RemoveRange(context.WeatherCaches);
        context.SaveChanges();
        foreach (var table in BackupTables.All)
        {
            table.AddAll(context, package.Rows(table.Name));
        }
        context.SaveChanges();
    }

    private static void Merge(ChiselDbContext context, BackupPackage package)
    {
        AddNew(context.Tasks, package.Rows(BackupTables.Tasks).Cast<TaskEntity>(), t => t.Id, t => t.Position, (t, p) => t.Position = p);
        AddNew(context.Notes, package.Rows(BackupTables.Notes).Cast<NoteEntity>(), n => n.Id);
        AddNew(context.QuickLinks, package.Rows(BackupTables.QuickLinks).Cast<QuickLinkEntity>(), l => l.Id, l => l.Position, (l, p) => l.Position = p);
        AddNew(context.Alarms, package.Rows(BackupTables.Alarms).Cast<AlarmEntity>(), a => a.Id);

        var usedSlots = context.LayoutProfiles.Select(p => p.Slot).ToHashSet();
        context.LayoutProfiles.AddRange(package.Rows(BackupTables.LayoutProfiles).Cast<LayoutProfileEntity>().Where(p => !usedSlots.Contains(p.Slot)));
        context.SaveChanges();

        if (Json<HabitsEntity>(package, BackupTables.Habits) is { } habitsJson)
        {
            var habits = new HabitRepository(context);
            var current = habits.Get().Habits.ToList();
            var ids = current.Select(h => h.Id).ToHashSet();
            var names = current.Select(h => h.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            foreach (var h in HabitRepository.Parse(habitsJson).Habits)
            {
                if (current.Count < HabitValidation.MaxHabits && ids.Add(h.Id) && names.Add(h.Name))
                {
                    current.Add(h);
                }
            }
            habits.Save(new HabitData(current));
        }

        if (Json<CountdownSettingsEntity>(package, BackupTables.Countdown) is { } countdownJson)
        {
            var countdown = new CountdownRepository(context);
            var settings = countdown.Get();
            var events = settings.Events.ToList();
            var ids = events.Select(e => e.Id).ToHashSet();
            events.AddRange(CountdownRepository.Parse(countdownJson).Events.Where(e => ids.Add(e.Id)));
            countdown.Save(settings with { Events = events.Take(CountdownValidation.MaxEvents).ToList() });
        }

        if (Json<ClockFaceLibraryEntity>(package, BackupTables.ClockFaces) is { } facesJson)
        {
            var faces = new ClockFaceRepository(context);
            var current = faces.Load().ToList();
            var ids = current.Select(f => f.Id).ToHashSet();
            current.AddRange(Core.Clock.ClockFaceJson.Parse(facesJson).Where(f => ids.Add(f.Id)));
            faces.Save(current);
        }
    }

    private static string? Json<T>(BackupPackage package, string table) where T : class =>
        package.Rows(table).Cast<T>().FirstOrDefault() switch
        {
            HabitsEntity h => h.Json,
            CountdownSettingsEntity c => c.Json,
            ClockFaceLibraryEntity f => f.Json,
            _ => null,
        };

    // Adds the rows whose id is not there yet; listed rows go after the ones already there, in their own order.
    private static void AddNew<T>(Microsoft.EntityFrameworkCore.DbSet<T> set, IEnumerable<T> rows, Func<T, Guid> id,
        Func<T, int>? position = null, Action<T, int>? setPosition = null) where T : class
    {
        var existing = set.AsEnumerable().ToList();
        var ids = existing.Select(id).ToHashSet();
        var next = position is null || existing.Count == 0 ? 0 : existing.Max(position) + 1;
        var added = rows.Where(r => ids.Add(id(r)));
        if (position is not null)
        {
            added = added.OrderBy(position);
        }
        foreach (var row in added.ToList())
        {
            setPosition?.Invoke(row, next++);
            set.Add(row);
        }
    }
}
