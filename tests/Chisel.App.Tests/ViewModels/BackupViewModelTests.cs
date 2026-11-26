using System.Text;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.Infrastructure.Transfer;

namespace Chisel.App.Tests.ViewModels;

public class BackupViewModelTests
{
    private sealed class FakeDialogs : IBackupDialogs
    {
        public string? SavePath { get; set; } = @"X:\out\backup.json";
        public string? OpenPath { get; set; } = @"X:\in\backup.json";
        public string? Suggested { get; private set; }
        public Task<string?> PickSaveAsync(string suggestedName)
        {
            Suggested = suggestedName;
            return Task.FromResult(SavePath);
        }
        public Task<string?> PickOpenAsync() => Task.FromResult(OpenPath);
    }

    private sealed class FakeService : IBackupService
    {
        public List<string> Calls { get; } = [];
        public BackupReadResult Read { get; set; } = BackupReadResult.Fail("unset");
        public ImportResult Result { get; set; } = new(true, "Imported.");
        public bool CanUndo { get; set; }
        public void Export(string path) => Calls.Add("export " + path);
        public BackupReadResult Inspect(string path) => Read;
        public ImportResult Import(BackupPackage package, ImportMode mode)
        {
            Calls.Add("import " + mode);
            return Result;
        }
        public ImportResult Undo()
        {
            Calls.Add("undo");
            return Result;
        }
    }

    private static BackupReadResult Package(string data) =>
        BackupDocument.Read(Encoding.UTF8.GetBytes("""{"app":"Chisel","formatVersion":1,"appVersion":"1.2.3","exportedAt":"2000-01-01T00:00:00Z","data":""" + data + "}"));

    private static (BackupViewModel Vm, FakeService Service, FakeDialogs Dialogs, List<string> Events) Create()
    {
        var service = new FakeService();
        var dialogs = new FakeDialogs();
        var events = new List<string>();
        var vm = new BackupViewModel(service, dialogs, beforeImport: () => events.Add("flush"), afterImport: () => events.Add("restart"),
            today: () => new DateOnly(2000, 1, 1));
        return (vm, service, dialogs, events);
    }

    [Fact]
    public async Task Export_SavesToThePickedFile()
    {
        var (vm, service, dialogs, _) = Create();

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal("chisel-backup-2000-01-01.json", dialogs.Suggested);
        Assert.Equal([@"export X:\out\backup.json"], service.Calls);
        Assert.Equal("Exported to backup.json.", vm.Status);
    }

    [Fact]
    public async Task Export_Cancelled_DoesNothing()
    {
        var (vm, service, dialogs, _) = Create();
        dialogs.SavePath = null;

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Empty(service.Calls);
    }

    [Fact]
    public async Task Import_ABadFile_ShowsWhyAndAsksNothing()
    {
        var (vm, service, _, _) = Create();
        service.Read = BackupReadResult.Fail("This backup is damaged and can't be read.");

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.False(vm.IsConfirming);
        Assert.Equal("This backup is damaged and can't be read.", vm.Status);
    }

    [Fact]
    public async Task Import_ShowsTheSummary_ThenReplaceFlushesImportsAndRestarts()
    {
        var (vm, service, _, events) = Create();
        service.Read = Package("""{"tasks":[{"id":"00000000-0000-0000-0000-000000000001","text":"A"}],"quickLinks":[{"id":"00000000-0000-0000-0000-000000000002","label":"Plan","url":"C:\\Data\\plan.txt"}]}""");

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.True(vm.IsConfirming);
        Assert.True(vm.CanMerge);
        Assert.Contains("0 widgets, 1 task, 1 quick link", vm.Summary);
        Assert.Contains("Chisel 1.2.3", vm.Summary);
        Assert.Single(vm.Warnings);

        vm.ReplaceCommand.Execute(null);

        Assert.Equal(["import Replace"], service.Calls);
        Assert.Equal(["flush", "restart"], events);
        Assert.False(vm.IsConfirming);
    }

    [Fact]
    public async Task Merge_IsOfferedOnlyWhenTheBackupHasListsToAdd()
    {
        var (vm, service, _, _) = Create();
        service.Read = Package("""{"userSettings":[]}""");

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.True(vm.IsConfirming);
        Assert.False(vm.CanMerge);
    }

    [Fact]
    public async Task AFailedImport_SaysSoAndDoesNotRestart()
    {
        var (vm, service, _, events) = Create();
        service.Read = Package("""{"tasks":[]}""");
        service.Result = new ImportResult(false, "The import failed, and nothing was changed.");

        await vm.ImportCommand.ExecuteAsync(null);
        vm.MergeCommand.Execute(null);

        Assert.Equal(["flush"], events);
        Assert.Equal("The import failed, and nothing was changed.", vm.Status);
    }

    [Fact]
    public void Undo_PutsTheOldDataBackAndRestarts()
    {
        var (vm, service, _, events) = Create();
        service.CanUndo = true;
        vm.Refresh();
        Assert.True(vm.CanUndo);

        vm.UndoCommand.Execute(null);

        Assert.Equal(["undo"], service.Calls);
        Assert.Equal(["flush", "restart"], events);
    }
}
