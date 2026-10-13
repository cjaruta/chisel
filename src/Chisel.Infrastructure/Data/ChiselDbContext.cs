using Microsoft.EntityFrameworkCore;

namespace Chisel.Infrastructure.Data;

public class ChiselDbContext : DbContext
{
    public ChiselDbContext(DbContextOptions<ChiselDbContext> options) : base(options)
    {
    }

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();

    public DbSet<ScheduleBlockEntity> ScheduleBlocks => Set<ScheduleBlockEntity>();

    public DbSet<NoteEntity> Notes => Set<NoteEntity>();

    public DbSet<MarkerEntity> Markers => Set<MarkerEntity>();
}

