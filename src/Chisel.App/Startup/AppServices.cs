using System;
using System.IO;
using System.Linq;
using Chisel.Core.Layout;
using Chisel.Core.Links;
using Chisel.Core.Notes;
using Chisel.Core.Pictures;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Chisel.Core.Tasks;
using Chisel.Core.Weather;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Pictures;
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
    public INotepadSettingsRepository NotepadSettings { get; }
    public IQuickLinkRepository QuickLinks { get; }
    public IMarkerRepository Markers { get; }
    public IScheduleRepository Schedule { get; }
    public IUserSettingsRepository UserSettings { get; }
    public IWidgetLayoutRepository WidgetLayout { get; }
    public IWeatherStore Weather { get; }
    public Chisel.Core.Clock.IClockStyleRepository ClockStyles { get; }
    public Chisel.Core.Clock.IClockFaceStore ClockFaces { get; }
    public Chisel.Core.Clock.IClockToolsRepository ClockTools { get; }
    public Chisel.Core.Clock.IAlarmRepository Alarms { get; }
    public Chisel.Core.Calculator.ICalculatorSettingsRepository CalculatorSettings { get; }
    public PictureCatalog Pictures { get; }
    public PictureImporter PictureImporter { get; }
    public IImageDownloader PictureDownloader { get; }
    public IPictureDropLog PictureDropLog { get; }

    // picturesFolder: where picture files go; by default a "pictures" folder next to the database.
    public AppServices(string dbPath, TimeProvider? time = null, string? picturesFolder = null)
    {
        Time = time ?? TimeProvider.System;

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var options = new DbContextOptionsBuilder<ChiselDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        DbContext = new ChiselDbContext(options);
        try
        {
            DatabaseInitializer.Initialize(DbContext);
        }
        catch
        {
            DbContext.Dispose();
            throw;
        }

        Tasks = new TaskRepository(DbContext);
        Notes = new NoteRepository(DbContext, Time);
        NotepadSettings = new NotepadSettingsRepository(DbContext);
        QuickLinks = new QuickLinkRepository(DbContext);
        Markers = new MarkerRepository(DbContext);
        Schedule = new ScheduleRepository(DbContext);
        UserSettings = new UserSettingsRepository(DbContext);
        WidgetLayout = new WidgetLayoutRepository(DbContext);
        Weather = new WeatherRepository(DbContext);
        ClockStyles = new ClockStyleRepository(DbContext);
        ClockFaces = new ClockFaceRepository(DbContext);
        ClockTools = new ClockToolsRepository(DbContext);
        Alarms = new AlarmRepository(DbContext);
        CalculatorSettings = new CalculatorSettingsRepository(DbContext);

        var folder = picturesFolder ?? Path.Combine(Path.GetDirectoryName(dbPath)!, "pictures");
        var files = new PictureFileStore(folder);
        var pictureRepository = new PictureRepository(DbContext);
        Pictures = new PictureCatalog(pictureRepository, files);
        PictureImporter = new PictureImporter(pictureRepository, files, new Services.AvaloniaImageResizer(), Time);
        PictureDropLog = new FilePictureDropLog(AppPaths.GetDropLogPath());
        PictureDownloader = HttpImageDownloader.CreateDefault(PictureDropLog);
        SweepPictures(folder);
    }

    // At startup: pictures an interrupted edit left behind (no frame uses them) are deleted. Never stops the app starting.
    private void SweepPictures(string folder)
    {
        try
        {
            Pictures.Sweep(Directory.Exists(folder) ? Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>().ToList() : []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException)
        {
            // The next start tries again.
        }
    }

    public void Dispose() => DbContext.Dispose();
}
