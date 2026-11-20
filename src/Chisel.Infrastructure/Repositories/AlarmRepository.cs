using Chisel.Core.Clock;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;

namespace Chisel.Infrastructure.Repositories;

public class AlarmRepository : IAlarmRepository
{
    private readonly ChiselDbContext _context;

    public AlarmRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public IReadOnlyList<Alarm> GetAll() => _context.Alarms.ToList().Select(a => a.ToDomain()).ToList();

    public void Save(Alarm alarm)
    {
        if (_context.Alarms.Find(alarm.Id) is { } existing)
        {
            _context.Entry(existing).CurrentValues.SetValues(alarm.ToEntity());
        }
        else
        {
            _context.Alarms.Add(alarm.ToEntity());
        }
        _context.SaveChanges();
    }

    public void Delete(Guid id)
    {
        if (_context.Alarms.Find(id) is { } entity)
        {
            _context.Alarms.Remove(entity);
            _context.SaveChanges();
        }
    }
}
