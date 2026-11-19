using System;
using System.Collections.Generic;

namespace Chisel.App.ViewModels;

public enum SettingsTab
{
    General,
    Widgets,
}

// One page in the Widgets tab. ViewType is a control whose bindings read from MainViewModel.
public sealed record SettingsPage(string Id, string Title, Type ViewType);

public static class SettingsPages
{
    // To give a widget options, add one entry here (see "How to add widget options" in docs/PROJECT_STATE.md).
    public static IReadOnlyList<SettingsPage> Widgets { get; } = new[]
    {
        new SettingsPage("schedule", "Schedule", typeof(Views.Pages.ScheduleSettingsPage)),
        new SettingsPage("weather", "Weather", typeof(Views.Pages.WeatherSettingsPage)),
        new SettingsPage("media", "Media", typeof(Views.Pages.MediaSettingsPage)),
    };
}
