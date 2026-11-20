using Chisel.Core.Clock;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;

namespace Chisel.Infrastructure.Repositories;

public class ClockStyleRepository : IClockStyleRepository
{
    private readonly ChiselDbContext _context;

    public ClockStyleRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public ClockStyle Get() => _context.ClockStyles.FirstOrDefault() is { } e ? e.ToDomain() : ClockStyle.Default;

    public void Save(ClockStyle style)
    {
        _context.ClockStyles.RemoveRange(_context.ClockStyles);
        _context.ClockStyles.Add(style.Normalized().ToEntity());
        _context.SaveChanges();
    }
}
