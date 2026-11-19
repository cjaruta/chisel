using Chisel.App.ViewModels;
using Chisel.Core.Settings;

namespace Chisel.App.Tests.ViewModels;

public class SettingsTabsTests
{
    private sealed class FakeSettingsRepository : IUserSettingsRepository
    {
        public UserSettings? Saved { get; private set; } = new("Test User", new TimeOnly(6, 30), new TimeOnly(23, 0));
        public UserSettings? Get() => Saved;
        public void Save(UserSettings settings) => Saved = settings;
    }

    private static SettingsViewModel Create(params SettingsPage[] pages) =>
        new(new FakeSettingsRepository(), new TimeDisplay(), pages: pages.Length == 0 ? null : pages);

    [Fact]
    public void Opens_OnTheGeneralTab()
    {
        var settings = Create();

        Assert.Equal(SettingsTab.General, settings.SelectedTab);
        Assert.True(settings.IsGeneralTab);
        Assert.False(settings.IsWidgetsTab);
    }

    [Fact]
    public void SelectTab_SwitchesBetweenGeneralAndWidgets()
    {
        var settings = Create();

        settings.SelectTabCommand.Execute(SettingsTab.Widgets);
        Assert.True(settings.IsWidgetsTab);
        Assert.False(settings.IsGeneralTab);

        settings.SelectTabCommand.Execute(SettingsTab.General);
        Assert.True(settings.IsGeneralTab);
    }

    [Fact]
    public void DefaultPages_ListScheduleWeatherThenMedia()
    {
        var settings = Create();

        Assert.Equal(new[] { "schedule", "weather", "media" }, settings.WidgetPages.Select(p => p.Id));
        Assert.Equal(new[] { "Schedule", "Weather", "Media" }, settings.WidgetPages.Select(p => p.Title));
        Assert.Same(settings.WidgetPages[0], settings.SelectedWidgetPage);
    }

    [Fact]
    public void RegisteredPages_AreListedAndSelectable()
    {
        var first = new SettingsPage("a", "Alpha", typeof(object));
        var second = new SettingsPage("b", "Beta", typeof(object));
        var settings = Create(first, second);

        Assert.Equal(new[] { first, second }, settings.WidgetPages);
        Assert.Same(first, settings.SelectedWidgetPage);

        settings.SelectedWidgetPage = second;
        Assert.Same(second, settings.SelectedWidgetPage);
    }

    [Fact]
    public void SettingsStillSaveAsBefore()
    {
        var repository = new FakeSettingsRepository();
        var settings = new SettingsViewModel(repository, new TimeDisplay());

        settings.UseTwelveHour = true;
        settings.SelectedPalette = settings.PaletteOptions[1];

        Assert.Equal(ClockFormat.TwelveHour, repository.Saved!.ClockFormat);
        Assert.Equal(settings.PaletteOptions[1].Id, repository.Saved.SchedulePalette);
    }
}
