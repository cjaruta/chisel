using System;
using System.Collections.Generic;

namespace Chisel.App.ViewModels;

public enum SettingsTab
{
    General,
    Widgets,
    QuickLinks,
}

// One page in the Widgets tab. ViewType is a control whose bindings read from MainViewModel.
// FillsHeight: the page lays itself out in the room it is given (and scrolls its own parts) instead of being scrolled as a whole,
// so it can keep something pinned at the top.
public sealed record SettingsPage(string Id, string Title, Type ViewType, bool FillsHeight = false);

public static class SettingsPages
{
    // To give a widget options, add one entry here (see "How to add widget options" in docs/PROJECT_STATE.md).
    public static IReadOnlyList<SettingsPage> Widgets { get; } = new[]
    {
        new SettingsPage("schedule", "Schedule", typeof(Views.Pages.ScheduleSettingsPage)),
        new SettingsPage("clock", "Clock", typeof(Views.Pages.ClockSettingsPage), FillsHeight: true),
        new SettingsPage("weather", "Weather", typeof(Views.Pages.WeatherSettingsPage)),
        new SettingsPage("media", "Media", typeof(Views.Pages.MediaSettingsPage)),
        new SettingsPage("pictureframe", "Picture frame", typeof(Views.Pages.PictureSettingsPage)),
        new SettingsPage("calculator", "Calculator", typeof(Views.Pages.CalculatorSettingsPage)),
    };
}
