using Chisel.Core.Settings;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class SystemSettingsRepository : ISystemSettingsRepository
{
    private readonly ChiselDbContext _context;

    public SystemSettingsRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public SystemSettings Get() => _context.SystemSettings.FirstOrDefault() is { } e
        ? new SystemSettings(e.CloseToTray, e.StartMinimized, e.Hotkey ?? "", e.ShowWelcome)
        : SystemSettings.Default;

    public void Save(SystemSettings settings)
    {
        _context.SystemSettings.RemoveRange(_context.SystemSettings);
        _context.SystemSettings.Add(new SystemSettingsEntity
        {
            CloseToTray = settings.CloseToTray, StartMinimized = settings.StartMinimized, Hotkey = settings.Hotkey, ShowWelcome = settings.ShowWelcome,
        });
        _context.SaveChanges();
    }
}
