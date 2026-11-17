using Chisel.Core.Layout;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;

namespace Chisel.Infrastructure.Repositories;

public class WidgetLayoutRepository : IWidgetLayoutRepository
{
    private readonly ChiselDbContext _context;

    public WidgetLayoutRepository(ChiselDbContext context)
    {
        _context = context;
    }

    // Null when no layout has been saved yet.
    public WidgetLayout? Get()
    {
        var placements = _context.WidgetPlacements.ToList();
        return placements.Count == 0 ? null : WidgetLayoutMapper.ToLayout(_context.LayoutColumns.ToList(), placements);
    }

    public void Save(WidgetLayout layout)
    {
        var (columns, placements) = layout.ToEntities();
        _context.WidgetPlacements.RemoveRange(_context.WidgetPlacements);
        _context.LayoutColumns.RemoveRange(_context.LayoutColumns);
        _context.SaveChanges();
        _context.LayoutColumns.AddRange(columns);
        _context.WidgetPlacements.AddRange(placements);
        _context.SaveChanges();
    }
}
