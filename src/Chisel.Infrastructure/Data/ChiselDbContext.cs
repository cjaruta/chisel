using Chisel.Core.Schedule;
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

    public DbSet<QuickLinkEntity> QuickLinks => Set<QuickLinkEntity>();

    public DbSet<UserSettingsEntity> UserSettings => Set<UserSettingsEntity>();

    public DbSet<WidgetPlacementEntity> WidgetPlacements => Set<WidgetPlacementEntity>();

    public DbSet<LayoutColumnEntity> LayoutColumns => Set<LayoutColumnEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Rows saved before blocks had colors get the neutral default.
        modelBuilder.Entity<ScheduleBlockEntity>()
            .Property(b => b.Color)
            .HasDefaultValue(BlockColor.Default);

        // Settings saved before palettes existed get the minimal palette.
        modelBuilder.Entity<UserSettingsEntity>()
            .Property(s => s.SchedulePalette)
            .HasDefaultValue(Core.Settings.UserSettings.DefaultSchedulePalette);

        // Layout columns are keyed by their index; widgets point at it with ColumnIndex.
        modelBuilder.Entity<LayoutColumnEntity>(column =>
        {
            column.HasKey(c => c.Position);
            column.Property(c => c.Position).ValueGeneratedNever();
            column.Property(c => c.WidthWeight).HasDefaultValue(1.0);
        });
        modelBuilder.Entity<WidgetPlacementEntity>()
            .Property(p => p.HeightWeight)
            .HasDefaultValue(1.0);
    }
}

