using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Chisel.App.Services;

// A text file the user picked: its file name and its whole text.
public sealed record NotepadFile(string Name, string Text);

// The notepad's two file actions, behind the native pickers. Both leave the pad alone: Open hands back a copy to import,
// Save writes a copy out. Neither remembers the file, so nothing is ever written back on its own.
public interface INotepadFiles
{
    // The picked file, or null when the picker was cancelled. Throws IOException, UnauthorizedAccessException or
    // InvalidDataException when the file cannot be read as text.
    Task<NotepadFile?> OpenAsync();

    // False when the picker was cancelled. Throws IOException or UnauthorizedAccessException when the write fails.
    Task<bool> SaveAsync(string suggestedName, string text);
}

// Reads and writes plain text exactly: UTF-8 out without a byte order mark, UTF-8 or a BOM-marked encoding in,
// and no changes to line endings, blank lines or indentation.
public static class TextFileCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static byte[] Encode(string text) => StrictUtf8.GetBytes(text);

    public static string Decode(Stream stream)
    {
        try
        {
            using var reader = new StreamReader(stream, StrictUtf8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return reader.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException("It is not a plain text file (UTF-8).");
        }
    }
}

// The real pickers. Does nothing (cancelled) when no window is available to host them.
public sealed class NotepadFileService(Func<IStorageProvider?> storage) : INotepadFiles
{
    private static readonly FilePickerFileType TextType = new("Text files") { Patterns = ["*.txt"], MimeTypes = ["text/plain"] };

    public async Task<NotepadFile?> OpenAsync()
    {
        if (storage() is not { CanOpen: true } provider)
        {
            return null;
        }
        var picked = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open text file",
            AllowMultiple = false,
            FileTypeFilter = [TextType, FilePickerFileTypes.All],
        });
        if (picked.FirstOrDefault() is not { } file)
        {
            return null;
        }
        await using var stream = await file.OpenReadAsync();
        return new NotepadFile(file.Name, TextFileCodec.Decode(stream));
    }

    public async Task<bool> SaveAsync(string suggestedName, string text)
    {
        if (storage() is not { CanSave: true } provider)
        {
            return false;
        }
        var target = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save as text file",
            SuggestedFileName = suggestedName,
            DefaultExtension = "txt",
            FileTypeChoices = [TextType],
            ShowOverwritePrompt = true,
        });
        if (target is null)
        {
            return false;
        }
        await using var stream = await target.OpenWriteAsync();
        stream.SetLength(0);
        var bytes = TextFileCodec.Encode(text);
        await stream.WriteAsync(bytes);
        return true;
    }
}
