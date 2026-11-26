using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Chisel.App.Services;
using Chisel.Infrastructure.Transfer;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// Settings > Backup: export everything to one file, import one (after a summary and a Replace or Merge choice), and undo the
// last import. beforeImport saves anything still pending (a note being typed) so the backup taken first has it; afterImport
// restarts Chisel so every widget loads the new data and nothing in memory writes the old data back.
public sealed partial class BackupViewModel : ObservableObject
{
    private readonly IBackupService _service;
    private readonly IBackupDialogs _dialogs;
    private readonly Action _beforeImport;
    private readonly Action _afterImport;
    private readonly Func<DateOnly> _today;
    private BackupPackage? _pending;

    public BackupViewModel(IBackupService service, IBackupDialogs dialogs, Action beforeImport, Action afterImport, Func<DateOnly>? today = null)
    {
        _service = service;
        _dialogs = dialogs;
        _beforeImport = beforeImport;
        _afterImport = afterImport;
        _today = today ?? (() => DateOnly.FromDateTime(DateTime.Now));
        Refresh();
    }

    [ObservableProperty]
    public partial string? Status { get; private set; }

    [ObservableProperty]
    public partial bool IsConfirming { get; private set; }

    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<string> Warnings { get; private set; } = [];

    [ObservableProperty]
    public partial bool CanMerge { get; private set; }

    [ObservableProperty]
    public partial bool CanUndo { get; private set; }

    public void Refresh() => CanUndo = _service.CanUndo;

    [RelayCommand]
    private async Task Export()
    {
        if (await _dialogs.PickSaveAsync($"chisel-backup-{_today():yyyy-MM-dd}.json") is not { } path)
        {
            return;
        }
        try
        {
            _service.Export(path);
            Status = $"Exported to {Path.GetFileName(path)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = "The backup couldn't be saved there. Try another folder.";
        }
    }

    [RelayCommand]
    private async Task Import()
    {
        Cancel();
        if (await _dialogs.PickOpenAsync() is not { } path)
        {
            return;
        }
        var read = _service.Inspect(path);
        if (read.Package is not { } package)
        {
            Status = read.Error;
            return;
        }
        _pending = package;
        var from = package.ExportedAt == default ? "" : $" on {package.ExportedAt.LocalDateTime:d}";
        Summary = $"Exported from Chisel {package.AppVersion}{from}: {package.Summary}.";
        Warnings = package.Warnings;
        CanMerge = package.CanMerge;
        Status = null;
        IsConfirming = true;
    }

    [RelayCommand]
    private void Replace() => Apply(ImportMode.Replace);

    [RelayCommand]
    private void Merge() => Apply(ImportMode.Merge);

    [RelayCommand]
    private void Cancel()
    {
        _pending = null;
        IsConfirming = false;
        Warnings = [];
    }

    [RelayCommand]
    private void Undo() => Run(() => _service.Undo());

    private void Apply(ImportMode mode)
    {
        if (_pending is not { } package)
        {
            return;
        }
        Cancel();
        Run(() => _service.Import(package, mode));
    }

    private void Run(Func<ImportResult> action)
    {
        _beforeImport();
        var result = action();
        Status = result.Message;
        Refresh();
        if (result.Succeeded)
        {
            _afterImport();
        }
    }
}
