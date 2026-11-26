using Chisel.Core.SystemMonitor;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class SystemMonitorSettingsRepository : ISystemMonitorSettingsRepository
{
    private readonly ChiselDbContext _context;

    public SystemMonitorSettingsRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public SystemMonitorSettings Get() =>
        _context.SystemMonitorSettings.FirstOrDefault() is { } e
            ? new SystemMonitorSettings((SystemStats)e.Shown, e.RefreshSeconds).Normalized()
            : SystemMonitorSettings.Default;

    public void Save(SystemMonitorSettings settings)
    {
        var s = settings.Normalized();
        _context.SystemMonitorSettings.RemoveRange(_context.SystemMonitorSettings);
        _context.SystemMonitorSettings.Add(new SystemMonitorSettingsEntity { Shown = (int)s.Shown, RefreshSeconds = s.RefreshSeconds });
        _context.SaveChanges();
    }
}
