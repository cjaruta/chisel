using Chisel.Core.Clipboard;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class ClipboardHistorySettingsRepository : IClipboardHistorySettingsRepository
{
    private readonly ChiselDbContext _context;

    public ClipboardHistorySettingsRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public ClipboardHistorySettings Get() => _context.ClipboardHistorySettings.FirstOrDefault() is { } e
        ? new ClipboardHistorySettings(e.KeepHistory, e.MaxItems, e.MaxLength).Valid()
        : ClipboardHistorySettings.Default;

    public void Save(ClipboardHistorySettings settings)
    {
        _context.ClipboardHistorySettings.RemoveRange(_context.ClipboardHistorySettings);
        _context.ClipboardHistorySettings.Add(new ClipboardHistorySettingsEntity
        {
            KeepHistory = settings.KeepHistory, MaxItems = settings.MaxItems, MaxLength = settings.MaxLength,
        });
        _context.SaveChanges();
    }
}
