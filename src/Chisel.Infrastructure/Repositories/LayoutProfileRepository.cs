using Chisel.Core.Layout;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;

namespace Chisel.Infrastructure.Repositories;

public class LayoutProfileRepository : ILayoutProfileRepository
{
    private readonly ChiselDbContext _context;

    public LayoutProfileRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public IReadOnlyList<LayoutProfile> GetAll() =>
        _context.LayoutProfiles.OrderBy(p => p.Slot).AsEnumerable()
            .Select(p => (p, Layout: LayoutProfileMapper.ToLayout(p.LayoutJson) ?? Damaged(p)))
            .Where(x => x.Layout is not null && LayoutProfiles.IsValidSlot(x.p.Slot))
            .Select(x => new LayoutProfile(x.p.Slot, x.p.Name, x.Layout!))
            .ToList();

    // A profile that can't be read is kept as a .bad copy and left out of the list.
    private WidgetLayout? Damaged(LayoutProfileEntity row)
    {
        DamagedData.Keep(_context, $"layout-profile-{row.Slot}", row.LayoutJson);
        return null;
    }

    public void Save(LayoutProfile profile)
    {
        if (!LayoutProfiles.IsValidSlot(profile.Slot))
        {
            throw new ArgumentOutOfRangeException(nameof(profile), profile.Slot, "There is no such profile slot.");
        }
        var row = _context.LayoutProfiles.Find(profile.Slot);
        if (row is null)
        {
            row = new LayoutProfileEntity { Slot = profile.Slot };
            _context.LayoutProfiles.Add(row);
        }
        row.Name = profile.Name;
        row.LayoutJson = profile.Layout.ToJson();
        _context.SaveChanges();
    }

    public void Delete(int slot)
    {
        if (_context.LayoutProfiles.Find(slot) is { } row)
        {
            _context.LayoutProfiles.Remove(row);
            _context.SaveChanges();
        }
    }
}
