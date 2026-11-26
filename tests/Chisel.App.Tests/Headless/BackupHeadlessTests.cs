using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.Infrastructure.Transfer;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.Headless;

public class BackupHeadlessTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "chisel-backup-ui-" + Guid.NewGuid().ToString("N"));

    private sealed class SameFile(string path) : IBackupDialogs
    {
        public Task<string?> PickSaveAsync(string suggestedName) => Task.FromResult<string?>(path);
        public Task<string?> PickOpenAsync() => Task.FromResult<string?>(path);
    }

    private static Control Named(MainWindow w, string name) =>
        w.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);

    [AvaloniaFact]
    public async Task ExportThenImport_ShowsTheSummaryAndTheReplaceOrMergeChoice()
    {
        var file = Path.Combine(_folder, "backup.json");
        var window = UiSnapshot.ShowMainWindow(backup: context => new BackupViewModel(
            new BackupService(context, new FileSystemBackupFiles(), Path.Combine(_folder, "undo"), Path.Combine(_folder, "pictures"), "1.0.0",
                new FakeTimeProvider(UiSnapshot.SampleNow)),
            new SameFile(file), () => { }, () => { }));
        var settings = ((MainViewModel)window.DataContext!).Settings;
        settings.IsOpen = true;
        settings.SelectTabCommand.Execute(SettingsTab.Backup);

        await settings.Backup!.ExportCommand.ExecuteAsync(null);
        await settings.Backup.ImportCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        UiSnapshot.Render(window, "backup-confirm");

        Assert.True(Named(window, "BackupConfirm").IsVisible);
        Assert.True(Named(window, "BackupMergeButton").IsVisible);
        Assert.Contains("tasks", ((TextBlock)Named(window, "BackupSummary")).Text);
        window.Close();
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
