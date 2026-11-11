using System;
using System.IO;
using Chisel.Core.Layout;
using Chisel.Core.Links;
using Chisel.Core.Notes;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Chisel.Core.Tasks;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Startup;
using Microsoft.EntityFrameworkCore;

namespace Chisel.App.Startup;

public sealed class AppServices : IDisposable
{
    public ChiselDbContext DbContext { get; }
    public TimeProvider Time { get; }
    public ITaskRepository Tasks { get; }
    public INoteRepository Notes { get; }
    public IQuickLinkRepository QuickLinks { get; }
    public IMarkerRepository Markers { get; }
    public IScheduleRepository Schedule { get; }
    public IUserSettingsRepository UserSettings { get; }
    public IWidgetLayoutRepository WidgetLayout { get; }

    public AppServices(string dbPath, TimeProvider? time = null)
    {
        Time = time ?? TimeProvider.System;

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var options = new DbContextOptionsBuilder<ChiselDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        DbContext = new ChiselDbContext(options);
        DatabaseInitializer.Initialize(DbContext);

        Tasks = new TaskRepository(DbContext);
        Notes = new NoteRepository(DbContext, Time);
        QuickLinks = new QuickLinkRepository(DbContext);
        Markers = new MarkerRepository(DbContext);
        Schedule = new ScheduleRepository(DbContext);
        UserSettings = new UserSettingsRepository(DbContext);
        WidgetLayout = new WidgetLayoutRepository(DbContext);
    }

    public void Dispose() => DbContext.Dispose();
}
