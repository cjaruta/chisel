using Chisel.Core.Schedule;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;

namespace Chisel.Infrastructure.Repositories;

public class MarkerRepository : IMarkerRepository
{
    private readonly ChiselDbContext _context;

    public MarkerRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public IReadOnlyList<Marker> GetAll() =>
        _context.Markers
            .ToList()
            .Select(m => m.ToDomain())
            .ToList();

    public void ReplaceAll(IEnumerable<Marker> markers)
    {
        _context.Markers.RemoveRange(_context.Markers);
        _context.Markers.AddRange(markers.Select(m => m.ToEntity()));
        _context.SaveChanges();
    }
}
