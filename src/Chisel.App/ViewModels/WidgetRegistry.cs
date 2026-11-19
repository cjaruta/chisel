using System;
using System.Collections.Generic;
using System.Linq;
using Chisel.App.Views.Widgets;
using Chisel.Core.Layout;

namespace Chisel.App.ViewModels;

// One widget the window can show. Id is stable (it is what layouts store), Title is its card heading,
// MinHeight is the least height its frame gets, ViewType is the view that draws its content.
// ScrollsContent: the frame scrolls the content when it is taller than the frame (the schedule scrolls itself).
// Icon is a 24x24 outline path (drawn as a stroke) for the tray; DefaultHeight is the height weight it gets when
// placed (in the default layout, or when added back from the tray). DefaultShowHeader: whether the card shows its
// title bar unless the user chose otherwise (see WidgetPlacement.ShowHeader).
public sealed record WidgetDefinition(
    string Id, string Title, string Icon, double DefaultHeight, double MinHeight, bool ScrollsContent, Type ViewType,
    bool DefaultShowHeader = true);

// Every widget, and the arrangement used when nothing is saved.
public static class WidgetRegistry
{
    public const string Schedule = "schedule";
    public const string Tasks = "tasks";
    public const string Notes = "notes";
    public const string Links = "links";
    public const string Clock = "clock";
    public const string Weather = "weather";
    public const string Media = "media";

    // The least height any widget's card gets, in pixels: its title bar plus one readable row of content.
    // Resizing stops here, and a column too short for its widgets' minimums scrolls.
    public const double MinimumHeight = 110;

    public static IReadOnlyList<WidgetDefinition> All { get; } = new[]
    {
        new WidgetDefinition(Schedule, "Today", Icon: "M4,5 H20 V20 H4 Z M4,9 H20 M8,3 V6 M16,3 V6",
            DefaultHeight: 1, MinimumHeight, ScrollsContent: false, typeof(ScheduleWidgetView), DefaultShowHeader: false),
        new WidgetDefinition(Tasks, "Tasks", Icon: "M4,6 L6,8 L9,4 M12,6 H20 M4,13 L6,15 L9,11 M12,13 H20 M5,19 H8 M12,19 H20",
            DefaultHeight: 1.05, MinimumHeight, ScrollsContent: true, typeof(TasksWidgetView)),
        new WidgetDefinition(Notes, "Notes", Icon: "M6,3 H15 L19,7 V21 H6 Z M9,11 H16 M9,15 H16 M9,18 H13",
            DefaultHeight: 1.1, MinimumHeight, ScrollsContent: true, typeof(NotesWidgetView)),
        new WidgetDefinition(Links, "Quick links", Icon: "M10,14 L14,10 M8,12 L6,14 A3,3 0 0 0 10,18 L12,16 M16,12 L18,10 A3,3 0 0 0 14,6 L12,8",
            DefaultHeight: 0.85, MinimumHeight, ScrollsContent: true, typeof(LinksWidgetView), DefaultShowHeader: false),
        new WidgetDefinition(Clock, "Clock", Icon: "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M12,7 V12 L15,14",
            DefaultHeight: 1, MinimumHeight, ScrollsContent: false, typeof(ClockWidgetView), DefaultShowHeader: false),
        new WidgetDefinition(Weather, "Weather", Icon: "M7,18 H17 A4,4 0 0 0 17,10 A6,6 0 0 0 6,12 A3,3 0 0 0 7,18",
            DefaultHeight: 1, MinimumHeight, ScrollsContent: false, typeof(WeatherWidgetView), DefaultShowHeader: false),
        new WidgetDefinition(Media, "Media", Icon: "M9,18 V6 L19,4 V16 M9,18 A2.5,2.5 0 1 1 4,18 A2.5,2.5 0 1 1 9,18 M19,16 A2.5,2.5 0 1 1 14,16 A2.5,2.5 0 1 1 19,16",
            DefaultHeight: 1, MinimumHeight, ScrollsContent: false, typeof(MediaWidgetView), DefaultShowHeader: false),
    };

    // Schedule on the left at full height; Tasks, Notes and Quick links stacked on the right.
    // Widths keep the old 1.3 : 1 split. Heights favour Tasks so its "New task" box stays in view at common sizes.
    public static WidgetLayout DefaultLayout { get; } = new(new[]
    {
        new LayoutColumn(1.3, new[] { Placement(Schedule) }),
        new LayoutColumn(1, new[] { Placement(Tasks), Placement(Notes), Placement(Links) }),
    }, hiddenWidgetIds: new[] { Clock, Weather, Media });

    public static WidgetDefinition? Get(string id) => All.FirstOrDefault(w => w.Id == id);

    private static WidgetPlacement Placement(string id) => new(id, All.First(w => w.Id == id).DefaultHeight);
}
