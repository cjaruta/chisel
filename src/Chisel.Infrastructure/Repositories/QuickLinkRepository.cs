using Chisel.Core.Links;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;

namespace Chisel.Infrastructure.Repositories;

public class QuickLinkRepository : IQuickLinkRepository
{
    private readonly ChiselDbContext _context;

    public QuickLinkRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public IReadOnlyList<QuickLink> GetAll() =>
        _context.QuickLinks
            .ToList()
            .Select(l => l.ToDomain())
            .ToList();

    public void Add(QuickLink link)
    {
        _context.QuickLinks.Add(link.ToEntity());
        _context.SaveChanges();
    }

    public void Remove(Guid id)
    {
        var entity = _context.QuickLinks.Find(id);
        if (entity is null)
        {
            return;
        }

        _context.QuickLinks.Remove(entity);
        _context.SaveChanges();
    }
}
