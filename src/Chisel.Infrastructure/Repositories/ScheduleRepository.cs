using Chisel.Core.Schedule;
using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Mapping;

namespace Chisel.Infrastructure.Repositories;

public class ScheduleRepository : IScheduleRepository
{
    private readonly ChiselDbContext _context;

    public ScheduleRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public DaySchedule Get() =>
        new(_context.ScheduleBlocks
            .OrderBy(b => b.Position)
            .ToList()
            .Select(b => b.ToDomain()));

    public void Save(DaySchedule schedule)
    {
        _context.ScheduleBlocks.RemoveRange(_context.ScheduleBlocks);
        _context.ScheduleBlocks.AddRange(schedule.Blocks.Select((b, position) =>
        {
            var entity = b.ToEntity();
            entity.Position = position;
            return entity;
        }));
        _context.SaveChanges();
    }
}
