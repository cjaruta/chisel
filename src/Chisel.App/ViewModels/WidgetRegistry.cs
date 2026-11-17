using System;
using System.Collections.Generic;
using System.Linq;
using Chisel.App.Views.Widgets;
using Chisel.Core.Layout;

namespace Chisel.App.ViewModels;

// One widget the window can show. Id is stable (it is what layouts store), Title is its card heading,
// MinHeight is the least height its frame gets, ViewType is the view that draws its content.
// ScrollsContent: the frame scrolls the content when it is taller than the frame (the schedule scrolls itself).
public sealed record WidgetDefinition(string Id, string Title, double MinHeight, bool ScrollsContent, Type ViewType);

// Every widget, and the arrangement used when nothing is saved.
public static class WidgetRegistry
{
    public const string Schedule = "schedule";
    public const string Tasks = "tasks";
    public const string Notes = "notes";
    public const string Links = "links";

    public static IReadOnlyList<WidgetDefinition> All { get; } = new[]
    {
        new WidgetDefinition(Schedule, "Today", 240, ScrollsContent: false, typeof(ScheduleWidgetView)),
        new WidgetDefinition(Tasks, "Tasks", 120, ScrollsContent: true, typeof(TasksWidgetView)),
        new WidgetDefinition(Notes, "Notes", 140, ScrollsContent: true, typeof(NotesWidgetView)),
        new WidgetDefinition(Links, "Quick links", 120, ScrollsContent: true, typeof(LinksWidgetView)),
    };

    // Schedule on the left at full height; Tasks, Notes and Quick links stacked on the right.
    // Widths keep the old 1.3 : 1 split. Heights favour Tasks so its "New task" box stays in view at common sizes.
    public static WidgetLayout DefaultLayout { get; } = new(new[]
    {
        new LayoutColumn(1.3, new[] { new WidgetPlacement(Schedule) }),
        new LayoutColumn(1, new[] { new WidgetPlacement(Tasks, 1.05), new WidgetPlacement(Notes, 1.1), new WidgetPlacement(Links, 0.85) }),
    });

    public static WidgetDefinition? Get(string id) => All.FirstOrDefault(w => w.Id == id);
}
