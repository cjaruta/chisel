using Chisel.Core.Notes;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class NotepadSettingsRepository : INotepadSettingsRepository
{
    private readonly ChiselDbContext _context;

    public NotepadSettingsRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public NotepadSettings Get() => _context.NotepadSettings.FirstOrDefault() is { } e
        ? new NotepadSettings(e.LastPadId, e.Monospace, e.WordWrap)
        : NotepadSettings.Default;

    public void Save(NotepadSettings settings)
    {
        _context.NotepadSettings.RemoveRange(_context.NotepadSettings);
        _context.NotepadSettings.Add(new NotepadSettingsEntity
        {
            LastPadId = settings.LastPadId,
            Monospace = settings.Monospace,
            WordWrap = settings.WordWrap,
        });
        _context.SaveChanges();
    }
}
