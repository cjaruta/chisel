using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Chisel.App.Services;

// Where to save an export and which backup to import. Null: cancelled.
public interface IBackupDialogs
{
    Task<string?> PickSaveAsync(string suggestedName);
    Task<string?> PickOpenAsync();
}

// The native pickers. Cancelled when no window can host them or the pick is not a local file.
public sealed class BackupDialogs(Func<IStorageProvider?> storage) : IBackupDialogs
{
    private static readonly FilePickerFileType BackupType = new("Chisel backup") { Patterns = ["*.json"], MimeTypes = ["application/json"] };

    public async Task<string?> PickSaveAsync(string suggestedName)
    {
        if (storage() is not { CanSave: true } provider)
        {
            return null;
        }
        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Chisel backup",
            SuggestedFileName = suggestedName,
            DefaultExtension = "json",
            FileTypeChoices = [BackupType],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickOpenAsync()
    {
        if (storage() is not { CanOpen: true } provider)
        {
            return null;
        }
        var picked = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Chisel backup",
            AllowMultiple = false,
            FileTypeFilter = [BackupType],
        });
        return picked.FirstOrDefault()?.TryGetLocalPath();
    }
}
