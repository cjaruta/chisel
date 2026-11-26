using Avalonia.Media;
using Chisel.App.Models;
using Chisel.App.ViewModels;
using Chisel.Core.Schedule;
using Chisel.Core.Settings;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.App.Tests.ViewModels;

// Choosing a color palette in settings: blocks, the pending block and the editor swatches
// re-resolve their colors right away, and the choice is saved and loaded on startup.
public class SchedulePaletteTests
{
    private sealed class FakeSettingsRepository : IUserSettingsRepository
    {
        public UserSettings? Saved { get; private set; }
        public int SaveCalls { get; private set; }
        public FakeSettingsRepository(UserSettings? initial) => Saved = initial;
        public UserSettings? Get() => Saved;
        public void Save(UserSettings settings)
        {
            Saved = settings;
            SaveCalls++;
        }
    }

    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public DaySchedule Saved { get; set; } = new(new[]
        {
            new ScheduleBlock("Focus", new TimeOnly(8, 0), new TimeOnly(11, 0), BlockColor.Slate),
            new ScheduleBlock("Workout", new TimeOnly(14, 0), new TimeOnly(15, 0), BlockColor.Sage),
        });
        public DaySchedule Get() => Saved;
        public void Save(DaySchedule schedule) => Saved = schedule;
    }

    private sealed class FakeMarkerRepository : IMarkerRepository
    {
        public IReadOnlyList<Marker> GetAll() => Array.Empty<Marker>();
        public void ReplaceAll(IEnumerable<Marker> markers) { }
    }

    private static readonly UserSettings Defaults = new("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0), ClockFormat.TwelveHour);

    private readonly PaletteDisplay _palette = new();
    private readonly TimeDisplay _display = new();

    private ScheduleViewModel CreateSchedule() =>
        new(new FakeScheduleRepository(), new FakeMarkerRepository(),
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 9, 30, 0, TimeSpan.Zero)), _display, _palette);

    private static Color ColorOf(IBrush brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    [Fact]
    public void Blocks_ResolveTheirColorsFromDefaultMinimalAtFirst()
    {
        var focus = CreateSchedule().GridBlocks[0];

        var expected = BlockPalettes.Minimal.Get(BlockColor.Slate);
        Assert.Equal(expected.Fill, ColorOf(focus.Brushes.Fill));
        Assert.Equal(expected.Stripe, ColorOf(focus.Brushes.Stripe));
        Assert.Equal(expected.Text, ColorOf(focus.Brushes.Text));
        Assert.Equal(BlockPalettes.Minimal.TimeText, ColorOf(focus.Brushes.TimeText));
    }

    [Fact]
    public void ChangingThePalette_ChangesTheColorsResolvedForBlocksRightAway()
    {
        var schedule = CreateSchedule();

        _palette.Use(BlockPalettes.Saturated.Id);

        var workout = schedule.GridBlocks[1];
        Assert.Equal(BlockPalettes.Saturated.Get(BlockColor.Sage).Fill, ColorOf(workout.Brushes.Fill));
        Assert.Equal(BlockPalettes.Saturated.Get(BlockColor.Sage).Stripe, ColorOf(workout.Brushes.Stripe));
        Assert.Equal(BlockColor.Sage, workout.ColorKey);
    }

    [Fact]
    public void ChangingThePalette_RecolorsThePendingBlockAndTheEditorSwatches()
    {
        var schedule = CreateSchedule();
        schedule.BeginDrag(720, 0);
        schedule.UpdateDrag(800, 0);
        schedule.EndDrag();
        schedule.BlockEditor.SelectColorCommand.Execute(BlockColor.Rose);

        _palette.Use(BlockPalettes.Saturated.Id);

        Assert.Equal(BlockPalettes.Saturated.Get(BlockColor.Rose).Fill, ColorOf(schedule.PreviewBrushes!.Fill));
        Assert.Equal(BlockPalettes.Saturated.Get(BlockColor.Rose).Stripe,
            ColorOf(schedule.BlockEditor.Swatches.Single(s => s.Key == BlockColor.Rose).Brush));
    }

    [Fact]
    public void Use_UnknownId_FallsBackToDefaultMinimal()
    {
        _palette.Use(BlockPalettes.Saturated.Id);

        _palette.Use("neon");

        Assert.Same(BlockPalettes.Minimal, _palette.Palette);
    }

    [Fact]
    public void Settings_OffersEveryPaletteWithAPreviewOfTheirEightColors()
    {
        var settings = new SettingsViewModel(new FakeSettingsRepository(Defaults), _display, _palette);

        Assert.Equal(new[] { "Default Minimal", "Default Saturated", "Pastel", "Earth", "Coastal", "Jewel" }, settings.PaletteOptions.Select(o => o.Name));
        var saturated = settings.PaletteOptions[1];
        Assert.Equal(BlockPalettes.Saturated.Swatches.Select(s => s.Stripe), saturated.Preview.Select(ColorOf));
    }

    [Fact]
    public void ChoosingAPalette_AppliesItAndSavesTheChoiceKeepingOtherSettings()
    {
        var repository = new FakeSettingsRepository(Defaults);
        var settings = new SettingsViewModel(repository, _display, _palette);

        settings.SelectedPalette = settings.PaletteOptions.Single(o => o.Id == BlockPalettes.Saturated.Id);

        Assert.Same(BlockPalettes.Saturated, _palette.Palette);
        Assert.Equal(BlockPalettes.Saturated.Id, repository.Saved!.SchedulePalette);
        Assert.Equal(ClockFormat.TwelveHour, repository.Saved.ClockFormat);
    }

    [Fact]
    public void ChoosingTheSamePaletteAgain_DoesNotSave()
    {
        var repository = new FakeSettingsRepository(Defaults);
        var settings = new SettingsViewModel(repository, _display, _palette);

        settings.SelectedPalette = settings.PaletteOptions[0];

        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public void Startup_LoadsTheSavedPaletteBeforeBlocksAreShown()
    {
        var settings = new SettingsViewModel(
            new FakeSettingsRepository(Defaults.WithSchedulePalette(BlockPalettes.Saturated.Id)), _display, _palette);

        var schedule = CreateSchedule();

        Assert.Equal(BlockPalettes.Saturated.Id, settings.SelectedPalette.Id);
        Assert.Same(BlockPalettes.Saturated, _palette.Palette);
        Assert.Equal(BlockPalettes.Saturated.Get(BlockColor.Slate).Fill, ColorOf(schedule.GridBlocks[0].Brushes.Fill));
    }

    [Theory]
    [InlineData("neon")]
    [InlineData(null)]
    public void Startup_WithAnUnknownOrNoSavedPalette_UsesDefaultMinimal(string? saved)
    {
        var repository = new FakeSettingsRepository(saved is null ? null : Defaults.WithSchedulePalette(saved));

        var settings = new SettingsViewModel(repository, _display, _palette);

        Assert.Equal(BlockPalettes.Minimal.Id, settings.SelectedPalette.Id);
        Assert.Same(BlockPalettes.Minimal, _palette.Palette);
    }
}

public class LightModeBlockBrushTests
{
    private static Color ColorOf(IBrush brush) => ((ISolidColorBrush)brush).Color;

    [Fact]
    public void InALightMode_BlocksAreSoftTintsWithDarkText_AndInADarkModeTheyAreUnchanged()
    {
        var display = new PaletteDisplay();
        var dark = display.Brushes(BlockColor.Slate);

        display.UseMode(Chisel.Core.Theme.ThemeModes.ById("light"));
        var light = display.Brushes(BlockColor.Slate);

        Assert.True(ColorOf(light.Fill).R > ColorOf(dark.Fill).R + 60, "the light fill is not much lighter");
        Assert.True(ColorOf(light.Text).R < 90, "the light mode title is not dark");
        Assert.True(ColorOf(dark.Text).R > 200, "the dark mode title changed");

        display.UseMode(Chisel.Core.Theme.ThemeModes.Default);
        Assert.Equal(ColorOf(dark.Fill), ColorOf(display.Brushes(BlockColor.Slate).Fill));
    }

    [Fact]
    public void ChangingTheMode_TellsTheViewsToResolveTheirBrushesAgain()
    {
        var display = new PaletteDisplay();
        var raised = 0;
        display.PropertyChanged += (_, _) => raised++;

        display.UseMode(Chisel.Core.Theme.ThemeModes.ById("light"));

        Assert.True(raised > 0);
    }
}
