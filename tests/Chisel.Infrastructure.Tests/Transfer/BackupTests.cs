using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using Chisel.Core.Clock;
using Chisel.Core.Countdown;
using Chisel.Core.Habits;
using Chisel.Core.Layout;
using Chisel.Core.Links;
using Chisel.Core.Notes;
using Chisel.Core.Tasks;
using Chisel.Infrastructure.Transfer;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Seeding;
using Chisel.Infrastructure.Tests.Data;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Chisel.Infrastructure.Tests.Transfer;

// An in-memory file system: paths are plain keys.
internal sealed class FakeBackupFiles : IBackupFiles
{
    public Dictionary<string, byte[]> Stored { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool FailWrites { get; set; }

    public bool Exists(string path) => Stored.ContainsKey(path);
    public long Length(string path) => Stored.TryGetValue(path, out var b) ? b.LongLength : throw new FileNotFoundException(path);
    public byte[] ReadAllBytes(string path) => Stored.TryGetValue(path, out var b) ? b : throw new FileNotFoundException(path);

    public void WriteAllText(string path, string text)
    {
        if (FailWrites)
        {
            throw new IOException("disk full");
        }
        Stored[path] = Encoding.UTF8.GetBytes(text);
    }

    public void Copy(string from, string to) => Stored[to] = ReadAllBytes(from);
    public void Delete(string path) => Stored.Remove(path);

    public void DeleteDirectory(string path)
    {
        foreach (var key in Stored.Keys.Where(k => k.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            Stored.Remove(key);
        }
    }

    public IReadOnlyList<string> Files(string directory, string pattern)
    {
        var prefix = pattern.TrimEnd('*').Split('*')[0];
        var suffix = pattern.Contains('*') ? pattern[(pattern.LastIndexOf('*') + 1)..] : "";
        return Stored.Keys
            .Where(k => string.Equals(Path.GetDirectoryName(k), directory, StringComparison.OrdinalIgnoreCase))
            .Where(k => Path.GetFileName(k).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && k.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

public class BackupTests
{
    private const string Backups = @"X:\Chisel\backups";
    private const string Pictures = @"X:\Chisel\pictures";
    private static readonly DateTimeOffset Now = new(2000, 1, 1, 9, 30, 0, TimeSpan.Zero);
    private static readonly string Hash = new('a', 64);

    private static ChiselDbContext Fresh(out SqliteConnectionFixture fixture)
    {
        fixture = new SqliteConnectionFixture();
        return fixture.CreateContext();
    }

    // Every table gets at least one row, through the repositories where the app has them.
    private static void Fill(ChiselDbContext c)
    {
        var time = new FakeTimeProvider(Now);
        new UserSettingsRepository(c).Save(SeedData.CreateUserSettings());
        new ScheduleRepository(c).Save(SeedData.CreateSchedule());
        new MarkerRepository(c).ReplaceAll(SeedData.CreateMarkers());
        var tasks = new TaskRepository(c);
        tasks.Add(new TaskItem("Write the plan"));
        tasks.Add(new TaskItem("Call back"));
        var notes = new NoteRepository(c, time);
        notes.Add(new Note(time, "Groceries", "Milk"));
        notes.Add(new Note(time, "Ideas", "More"));
        var links = new QuickLinkRepository(c);
        links.Add(new QuickLink("Docs", "https://docs.example.com"));
        links.Add(new QuickLink("Plan", @"C:\Data\plan.txt"));
        new AlarmRepository(c).Save(new Alarm(Guid.NewGuid(), new TimeOnly(6, 45), "Wake", true, AlarmDays.Monday, null));
        new HabitRepository(c).Save(new HabitData([new Habit(Guid.NewGuid(), "Read", Done: [new DateOnly(2000, 1, 1)])]));
        new CountdownRepository(c).Save(new CountdownSettings([new CountdownEntry(Guid.NewGuid(), "Trip", new DateOnly(2000, 2, 1))]));
        new ClockFaceRepository(c).Save([new CustomClockFace(Guid.NewGuid(), "Mine", ClockStyle.Default, ClockSlotsConfig.Default)]);
        new WidgetLayoutRepository(c).Save(new WidgetLayout(
            [new LayoutColumn(1, [new WidgetPlacement("tasks", 1), new WidgetPlacement("pictureframe:1", 1)])], hiddenWidgetIds: ["clock"]));
        new LayoutProfileRepository(c).Save(new LayoutProfile(1, "Work", new WidgetLayout([new LayoutColumn(1, [new WidgetPlacement("notes", 1)])])));
        new SystemSettingsRepository(c).Save(new Core.Settings.SystemSettings(true, false, "Ctrl+Alt+C"));
        new NotepadSettingsRepository(c).Save(new Core.Notes.NotepadSettings(null, true, false));
        c.WeatherLocations.Add(new WeatherLocationEntity { Name = "Testville", Latitude = 1, Longitude = 2 });
        c.WeatherCaches.Add(new WeatherCacheEntity { FetchedAt = Now, SnapshotJson = "{}" });
        c.ClockStyles.Add(new ClockStyleEntity());
        c.ClockTools.Add(new ClockToolsEntity());
        c.FocusTimer.Add(new FocusTimerEntity());
        c.CalculatorSettings.Add(new CalculatorSettingsEntity());
        c.ConverterSettings.Add(new ConverterSettingsEntity());
        c.CalendarSettings.Add(new CalendarSettingsEntity());
        c.SystemMonitorSettings.Add(new SystemMonitorSettingsEntity());
        c.ClipboardHistorySettings.Add(new ClipboardHistorySettingsEntity());
        var asset = new PictureAssetEntity
        {
            Id = Guid.NewGuid(), Hash = Hash, FileName = "photo.png", MimeType = "image/png", Width = 10, Height = 10, Bytes = 100,
            SourceUrl = @"C:\Data\photo.png", AddedAt = Now,
        };
        c.PictureAssets.Add(asset);
        c.FramePictures.Add(new FramePictureEntity { Id = Guid.NewGuid(), FrameId = "pictureframe:1", AssetId = asset.Id });
        c.FrameOptions.Add(new FrameOptionsEntity { FrameId = "pictureframe:1" });
        c.SaveChanges();
        c.ChangeTracker.Clear();
    }

    private static BackupService Service(ChiselDbContext c, FakeBackupFiles files, FakeTimeProvider? time = null) =>
        new(c, files, Backups, Pictures, "1.2.3", time ?? new FakeTimeProvider(Now));

    private static JsonObject Data(ChiselDbContext c) => (JsonObject)JsonNode.Parse(BackupDocument.Export(c, "1.2.3", Now))!["data"]!;

    private static BackupPackage Read(string json) => BackupDocument.Read(Encoding.UTF8.GetBytes(json)).Package!;

    [Fact]
    public void Export_HasTheHeaderAndEveryTable_WithoutLocalSourcePathsOrClipboardHistory()
    {
        using var c = Fresh(out var fixture);
        Fill(c);

        var root = JsonNode.Parse(BackupDocument.Export(c, "1.2.3", Now))!.AsObject();

        Assert.Equal("Chisel", root["app"]!.GetValue<string>());
        Assert.Equal(BackupDocument.FormatVersion, root["formatVersion"]!.GetValue<int>());
        Assert.Equal("1.2.3", root["appVersion"]!.GetValue<string>());
        Assert.Equal(Now, DateTimeOffset.Parse(root["exportedAt"]!.GetValue<string>()));
        var data = root["data"]!.AsObject();
        Assert.All(data, t => Assert.NotEmpty(t.Value!.AsArray()));
        Assert.DoesNotContain(data, t => t.Key.Contains("clipboardHistory", StringComparison.OrdinalIgnoreCase) || t.Key.Contains("weatherCache"));
        Assert.Null(data["pictureAssets"]![0]!["sourceUrl"]);
        // The picture and the local quick link are marked as tied to this computer.
        Assert.Equal(["pictureFile", "localPath"], root["machineBound"]!.AsArray().Select(m => m!["kind"]!.GetValue<string>()));
        fixture.Dispose();
    }

    [Fact]
    public void ReplaceImport_RoundTripsEveryTable()
    {
        using var source = Fresh(out var a);
        Fill(source);
        var json = BackupDocument.Export(source, "1.2.3", Now);
        using var target = Fresh(out var b);
        new TaskRepository(target).Add(new TaskItem("Old task"));

        BackupImporter.Apply(target, Read(json), ImportMode.Replace);

        Assert.Equal(Data(source).ToJsonString(), Data(target).ToJsonString());
        Assert.Equal(["Call back", "Write the plan"], new TaskRepository(target).GetAll().Select(t => t.Text).Order());
        Assert.Equal("Read", Assert.Single(new HabitRepository(target).Get().Habits).Name);
        Assert.Equal("Trip", Assert.Single(new CountdownRepository(target).Get().Events).Name);
        Assert.Equal("Mine", Assert.Single(new ClockFaceRepository(target).Load()).Name);
        Assert.Empty(target.WeatherCaches);
        a.Dispose();
        b.Dispose();
    }

    [Fact]
    public void MergeImport_AddsListsAndKeepsTheCurrentDashboardAndSettings()
    {
        using var source = Fresh(out var a);
        Fill(source);
        var json = BackupDocument.Export(source, "1.2.3", Now);
        using var target = Fresh(out var b);
        new TaskRepository(target).Add(new TaskItem("Mine"));
        new HabitRepository(target).Save(new HabitData([new Habit(Guid.NewGuid(), "Walk")]));
        var layout = new WidgetLayout([new LayoutColumn(1, [new WidgetPlacement("clock", 1)])]);
        new WidgetLayoutRepository(target).Save(layout);

        BackupImporter.Apply(target, Read(json), ImportMode.Merge);

        Assert.Equal(["Mine", "Write the plan", "Call back"], new TaskRepository(target).GetAll().Select(t => t.Text));
        Assert.Equal(["Walk", "Read"], new HabitRepository(target).Get().Habits.Select(h => h.Name));
        Assert.Equal(2, new NoteRepository(target, TimeProvider.System).GetAll().Count);
        Assert.Single(new CountdownRepository(target).Get().Events);
        Assert.Single(new ClockFaceRepository(target).Load());
        Assert.Equal(["clock"], new WidgetLayoutRepository(target).Get()!.WidgetIds);
        Assert.Null(new UserSettingsRepository(target).Get());
        Assert.Empty(target.PictureAssets);

        // Merging the same file again adds nothing twice.
        BackupImporter.Apply(target, Read(json), ImportMode.Merge);
        Assert.Equal(3, new TaskRepository(target).GetAll().Count);
        a.Dispose();
        b.Dispose();
    }

    [Fact]
    public void AFailingImport_ChangesNothing()
    {
        using var target = Fresh(out var fixture);
        Fill(target);
        var before = Data(target).ToJsonString();
        // Two pictures with the same hash break a unique index, but only once rows are being written.
        var json = JsonNode.Parse(BackupDocument.Export(target, "1.2.3", Now))!;
        var pictures = json["data"]!["pictureAssets"]!.AsArray();
        var copy = pictures[0]!.DeepClone();
        copy["id"] = Guid.NewGuid().ToString();
        pictures.Add(copy);

        Assert.ThrowsAny<Exception>(() => BackupImporter.Apply(target, Read(json.ToJsonString()), ImportMode.Replace));

        Assert.Equal(before, Data(target).ToJsonString());
        fixture.Dispose();
    }

    [Theory]
    [InlineData("{ not json", "damaged")]
    [InlineData("MZ\u0090\u0000binary", "isn't a Chisel backup")]
    [InlineData("""{"app":"Other","formatVersion":1,"data":{}}""", "isn't a Chisel backup")]
    [InlineData("""{"app":"Chisel","formatVersion":99,"appVersion":"9.0","data":{}}""", "newer version of Chisel (9.0)")]
    [InlineData("""{"app":"Chisel","formatVersion":0,"data":{}}""", "isn't recognized")]
    [InlineData("""{"app":"Chisel","formatVersion":1,"data":{"tasks":[{"id":"not a guid"}]}}""", "damaged")]
    [InlineData("""{"app":"Chisel","formatVersion":1,"data":{"secrets":[]}}""", "damaged")]
    [InlineData("""{"app":"Chisel","formatVersion":1,"data":{"userSettings":[{},{}]}}""", "damaged")]
    public void Read_RefusesBadFiles(string content, string expected)
    {
        var result = BackupDocument.Read(Encoding.UTF8.GetBytes(content));

        Assert.Null(result.Package);
        Assert.Contains(expected, result.Error);
    }

    [Fact]
    public void Read_RefusesAnOversizedFile()
    {
        var result = BackupDocument.Read(new byte[BackupDocument.MaxBytes + 1]);

        Assert.Contains("too large", result.Error);
    }

    [Theory]
    [InlineData("..\\..\\Windows\\win.ini", "photo.png")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "..\\..\\evil.png")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "C:/evil.png")]
    public void Read_RefusesPictureNamesThatLeaveThePicturesFolder(string hash, string fileName)
    {
        var row = new JsonObject { ["id"] = Guid.NewGuid().ToString(), ["hash"] = hash, ["fileName"] = fileName };
        var json = """{"app":"Chisel","formatVersion":1,"data":{"pictureAssets":[""" + row.ToJsonString() + "]}}";

        Assert.Contains("damaged", BackupDocument.Read(Encoding.UTF8.GetBytes(json)).Error);
    }

    [Fact]
    public void Read_AnOlderFileWithoutANewerColumn_UsesItsDefault()
    {
        var json = """{"app":"Chisel","formatVersion":1,"data":{"systemSettings":[{"id":1,"closeToTray":true,"hotkey":""}]}}""";

        var package = BackupDocument.Read(Encoding.UTF8.GetBytes(json)).Package!;
        using var target = Fresh(out var fixture);
        BackupImporter.Apply(target, package, ImportMode.Replace);

        var settings = new SystemSettingsRepository(target).Get();
        Assert.True(settings.CloseToTray);
        Assert.False(settings.ShowWelcome);
        fixture.Dispose();
    }

    [Fact]
    public void Read_SummarizesAndWarnsAboutFilesThatMayBeMissing()
    {
        using var c = Fresh(out var fixture);
        Fill(c);
        var bytes = Encoding.UTF8.GetBytes(BackupDocument.Export(c, "1.2.3", Now));

        var package = BackupDocument.Read(bytes, pictureExists: _ => false).Package!;

        Assert.Equal("2 widgets, 2 notes pads, 2 tasks, 2 quick links, 1 alarm, 1 habit, 1 countdown event, 1 clock face, 1 layout profile, 1 picture",
            package.Summary.ToString());
        Assert.True(package.CanMerge);
        Assert.Contains(package.Warnings, w => w.StartsWith("1 of 1 pictures aren't on this computer"));
        Assert.Contains(package.Warnings, w => w.StartsWith("1 quick links open files or folders"));
        Assert.True(BackupDocument.Read(bytes, pictureExists: _ => true).Package!.Warnings.All(w => !w.Contains("pictures")));
        fixture.Dispose();
    }

    [Fact]
    public void Read_LeavesOutQuickLinksTheAppWouldRefuse()
    {
        JsonObject Link(string label, string url) => new() { ["id"] = Guid.NewGuid().ToString(), ["label"] = label, ["url"] = url };
        var links = new JsonArray(Link("Tool", @"C:\Tools\run.exe"), Link("Script", "javascript:alert(1)"), Link("Docs", "https://docs.example.com"));
        var json = """{"app":"Chisel","formatVersion":1,"data":{"quickLinks":""" + links.ToJsonString() + "}}";
        using var target = Fresh(out var fixture);

        var package = BackupDocument.Read(Encoding.UTF8.GetBytes(json)).Package!;
        BackupImporter.Apply(target, package, ImportMode.Replace);

        Assert.Equal(1, package.Summary.QuickLinks);
        Assert.Contains(package.Warnings, w => w.StartsWith("2 quick links were left out"));
        Assert.Equal(["Docs"], new QuickLinkRepository(target).GetAll().Select(l => l.Label));
        fixture.Dispose();
    }

    [Fact]
    public void Import_SavesAnUndoPointWithThePictureFiles_AndUndoPutsEverythingBack()
    {
        using var source = Fresh(out var a);
        new TaskRepository(source).Add(new TaskItem("From the backup"));
        var json = BackupDocument.Export(source, "1.2.3", Now);
        using var target = Fresh(out var b);
        Fill(target);
        var before = Data(target).ToJsonString();
        var files = new FakeBackupFiles();
        var picture = Path.Combine(Pictures, Hash + ".png");
        files.Stored[picture] = [1, 2, 3];
        var service = Service(target, files);

        var result = service.Import(Read(json), ImportMode.Replace);
        Assert.True(result.Succeeded);
        Assert.Equal("From the backup", Assert.Single(new TaskRepository(target).GetAll()).Text);
        Assert.True(service.CanUndo);

        files.Stored.Remove(picture);
        var undo = service.Undo();

        Assert.True(undo.Succeeded);
        Assert.Equal(before, Data(target).ToJsonString());
        Assert.Equal([1, 2, 3], files.Stored[picture]);
        Assert.False(service.CanUndo);
        a.Dispose();
        b.Dispose();
    }

    [Fact]
    public void Import_WhenTheUndoPointCannotBeSaved_ImportsNothing()
    {
        using var source = Fresh(out var a);
        new TaskRepository(source).Add(new TaskItem("From the backup"));
        var json = BackupDocument.Export(source, "1.2.3", Now);
        using var target = Fresh(out var b);
        new TaskRepository(target).Add(new TaskItem("Mine"));

        var result = Service(target, new FakeBackupFiles { FailWrites = true }).Import(Read(json), ImportMode.Replace);

        Assert.False(result.Succeeded);
        Assert.Equal("Mine", Assert.Single(new TaskRepository(target).GetAll()).Text);
        a.Dispose();
        b.Dispose();
    }

    [Fact]
    public void Import_KeepsOnlyTheLatestUndoPoints()
    {
        using var target = Fresh(out var fixture);
        var files = new FakeBackupFiles();
        var time = new FakeTimeProvider(Now);
        var service = Service(target, files, time);
        var package = Read(BackupDocument.Export(target, "1.2.3", Now));

        for (var i = 0; i < BackupService.UndoPointsKept + 2; i++)
        {
            service.Import(package, ImportMode.Replace);
            time.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Equal(BackupService.UndoPointsKept, files.Files(Backups, "before-import-*.json").Count);
        fixture.Dispose();
    }

    [Fact]
    public void ExportThenInspect_ThroughTheService_ReadsTheSameFile()
    {
        using var c = Fresh(out var fixture);
        Fill(c);
        var files = new FakeBackupFiles();
        var service = Service(c, files);

        service.Export(@"X:\out\chisel.json");
        var result = service.Inspect(@"X:\out\chisel.json");

        Assert.NotNull(result.Package);
        Assert.Equal("1.2.3", result.Package!.AppVersion);
        Assert.Equal(Now, result.Package.ExportedAt);
        fixture.Dispose();
    }
}
