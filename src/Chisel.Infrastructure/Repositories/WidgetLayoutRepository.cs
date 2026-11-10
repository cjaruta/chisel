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

    public WidgetLayout Get() =>
        _context.WidgetPlacements.ToList().ToLayout();

    public void Save(WidgetLayout layout)
    {
        _context.WidgetPlacements.RemoveRange(_context.WidgetPlacements);
        _context.WidgetPlacements.AddRange(layout.ToEntities());
        _context.SaveChanges();
    }
}
