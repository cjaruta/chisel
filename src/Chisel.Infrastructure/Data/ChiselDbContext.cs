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

    public DbSet<HiddenWidgetEntity> HiddenWidgets => Set<HiddenWidgetEntity>();

    public DbSet<LayoutProfileEntity> LayoutProfiles => Set<LayoutProfileEntity>();

    public DbSet<PictureAssetEntity> PictureAssets => Set<PictureAssetEntity>();

    public DbSet<FramePictureEntity> FramePictures => Set<FramePictureEntity>();

    public DbSet<FrameOptionsEntity> FrameOptions => Set<FrameOptionsEntity>();

    public DbSet<WeatherLocationEntity> WeatherLocations => Set<WeatherLocationEntity>();

    public DbSet<WeatherCacheEntity> WeatherCaches => Set<WeatherCacheEntity>();
    public DbSet<ClockStyleEntity> ClockStyles => Set<ClockStyleEntity>();
    public DbSet<ClockFaceLibraryEntity> ClockFaceLibraries => Set<ClockFaceLibraryEntity>();
    public DbSet<ClockToolsEntity> ClockTools => Set<ClockToolsEntity>();
    public DbSet<AlarmEntity> Alarms => Set<AlarmEntity>();
    public DbSet<CalculatorSettingsEntity> CalculatorSettings => Set<CalculatorSettingsEntity>();
    public DbSet<NotepadSettingsEntity> NotepadSettings => Set<NotepadSettingsEntity>();

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

        // Saved layout profiles are keyed by their slot (1 to 3).
        modelBuilder.Entity<LayoutProfileEntity>().HasKey(p => p.Slot);
        modelBuilder.Entity<LayoutProfileEntity>().Property(p => p.Slot).ValueGeneratedNever();

        // One row per widget taken off the dashboard.
        modelBuilder.Entity<HiddenWidgetEntity>().HasKey(h => h.WidgetId);

        // The same bytes are stored once (one asset per hash); a frame has one options row and a numbered list of pictures.
        modelBuilder.Entity<PictureAssetEntity>().HasIndex(a => a.Hash).IsUnique();
        modelBuilder.Entity<FramePictureEntity>().HasIndex(p => p.FrameId);
        modelBuilder.Entity<FrameOptionsEntity>().HasKey(o => o.FrameId);

        // Settings saved before the spacing option keep the spacing the app always had.
        modelBuilder.Entity<UserSettingsEntity>().Property(s => s.WidgetSpacing).HasDefaultValue(Core.Settings.UserSettings.DefaultWidgetSpacing);

        // Settings saved before picture frames keep original files and make one slideshow of several pictures.
        modelBuilder.Entity<UserSettingsEntity>().Property(s => s.PictureKeepOriginals).HasDefaultValue(true);
        modelBuilder.Entity<UserSettingsEntity>().Property(s => s.PictureMultiDrop).HasDefaultValue(0);

        // Single-row tables: the saved weather place and its last snapshot.
        modelBuilder.Entity<WeatherLocationEntity>().Property(w => w.Id).ValueGeneratedNever();
        modelBuilder.Entity<WeatherCacheEntity>().Property(w => w.Id).ValueGeneratedNever();
        modelBuilder.Entity<ClockStyleEntity>().Property(c => c.Id).ValueGeneratedNever();
        modelBuilder.Entity<ClockFaceLibraryEntity>().Property(c => c.Id).ValueGeneratedNever();
        modelBuilder.Entity<ClockToolsEntity>().Property(c => c.Id).ValueGeneratedNever();
        modelBuilder.Entity<CalculatorSettingsEntity>().Property(c => c.Id).ValueGeneratedNever();
        modelBuilder.Entity<NotepadSettingsEntity>().Property(c => c.Id).ValueGeneratedNever();
    }
}

