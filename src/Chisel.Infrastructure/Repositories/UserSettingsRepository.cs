using Chisel.Core.Settings;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;

namespace Chisel.Infrastructure.Repositories;

public class UserSettingsRepository : IUserSettingsRepository
{
    private readonly ChiselDbContext _context;

    public UserSettingsRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public UserSettings? Get() =>
        _context.UserSettings.FirstOrDefault()?.ToDomain();

    public void Save(UserSettings settings)
    {
        _context.UserSettings.RemoveRange(_context.UserSettings);
        _context.UserSettings.Add(settings.ToEntity());
        _context.SaveChanges();
    }
}
