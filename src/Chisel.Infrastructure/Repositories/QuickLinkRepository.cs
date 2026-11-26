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
            .OrderBy(l => l.Position)
            .ToList()
            .Select(l => l.ToDomain())
            .ToList();

    public void Add(QuickLink link)
    {
        var entity = link.ToEntity();
        entity.Position = (_context.QuickLinks.Max(l => (int?)l.Position) ?? -1) + 1;
        _context.QuickLinks.Add(entity);
        _context.SaveChanges();
    }

    public void Update(QuickLink link)
    {
        var entity = _context.QuickLinks.Find(link.Id)
            ?? throw new InvalidOperationException($"Quick link {link.Id} does not exist.");

        entity.Label = link.Label;
        entity.Url = link.Url;
        _context.SaveChanges();
    }

    public void Reorder(IReadOnlyList<Guid> orderedIds)
    {
        var entities = _context.QuickLinks.ToDictionary(l => l.Id);
        var position = 0;
        foreach (var id in orderedIds)
        {
            if (entities.Remove(id, out var entity))
            {
                entity.Position = position++;
            }
        }
        // Anything not listed keeps its relative order after the listed ones.
        foreach (var entity in entities.Values.OrderBy(l => l.Position))
        {
            entity.Position = position++;
        }
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
