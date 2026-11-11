using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.Core.Links;
using Chisel.Core.Notes;
using Chisel.Core.Schedule;
using Chisel.Core.Tasks;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

// Builds the real MainWindow over seeded in-memory SQLite, shows it headlessly,
// and renders it to artifacts/ui/<name>.png so the UI can be checked without launching the app.
// Call from an [AvaloniaFact] test.
public static class UiSnapshot
{
    // 09:30 on 2000-01-01, so "Focus" (08:00-11:00) is the current block.
    public static readonly DateTimeOffset SampleNow = new(2000, 1, 1, 9, 30, 0, TimeSpan.Zero);

    // now: the clock time to show; defaults to SampleNow.
    public static MainWindow ShowMainWindow(double width = 1200, double height = 760, DateTimeOffset? now = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var context = new ChiselDbContext(new DbContextOptionsBuilder<ChiselDbContext>().UseSqlite(connection).Options);
        context.Database.Migrate();

        var time = new FakeTimeProvider(now ?? SampleNow);
        var schedule = new ScheduleRepository(context);
        var markers = new MarkerRepository(context);
        var tasks = new TaskRepository(context);
        var notes = new NoteRepository(context, time);
        var links = new QuickLinkRepository(context);
        Seed(schedule, markers, tasks, notes, links, time);

        var viewModel = new MainViewModel(
            new ClockViewModel(time),
            new ScheduleViewModel(schedule, markers, time),
            new TaskListViewModel(tasks),
            new NotesViewModel(notes, time),
            new QuickLinksViewModel(links, _ => { }),
            schedule,
            markers);

        var window = new MainWindow(windowPlacementPath: null)
        {
            DataContext = viewModel,
            Width = width,
            Height = height,
        };
        window.Closed += (_, _) =>
        {
            context.Dispose();
            connection.Dispose();
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    public static string Render(MainWindow window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("Headless rendering returned no frame; is UseHeadlessDrawing off?");

        var directory = Path.Combine(FindRepoRoot(), "artifacts", "ui");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".png");
        using (var file = File.Create(path))
        {
            frame.Save(file, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        }
        return path;
    }

    private static void Seed(
        IScheduleRepository schedule, IMarkerRepository markers, ITaskRepository tasks,
        INoteRepository notes, IQuickLinkRepository links, TimeProvider time)
    {
        schedule.Save(new DaySchedule(new[]
        {
            new ScheduleBlock("Wake up", new TimeOnly(6, 30), new TimeOnly(7, 0)),
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0)),
            new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0)),
            new ScheduleBlock("Sleep", new TimeOnly(23, 0), new TimeOnly(6, 30)),
        }));
        markers.ReplaceAll(new[] { new Marker("Last coffee", new TimeOnly(13, 0), MarkerKind.Checkpoint) });

        tasks.Add(new TaskItem("Write the weekly plan"));
        var done = new TaskItem("Water the plants");
        done.Complete();
        tasks.Add(done);
        tasks.Add(new TaskItem("Reply to messages"));

        notes.Add(new Note(time, "Ideas", "Try a shorter morning focus block."));
        notes.Add(new Note(time, "Groceries", "Milk, bread, apples"));

        links.Add(new QuickLink("Example", "https://example.com"));
        links.Add(new QuickLink("Docs", "https://example.com/docs"));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Chisel.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Couldn't find Chisel.slnx above the test output folder.");
    }
}
