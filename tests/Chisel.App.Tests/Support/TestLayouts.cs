using Chisel.Core.Layout;

namespace Chisel.App.Tests;

// Many layout tests need a right column of three stacked widgets, the way the dashboard used to start (before Quick links
// moved to the top bar). This is that starting point, with the daily verse as the third card.
public static class TestLayouts
{
    public static WidgetLayout Classic => new(new[]
    {
        new LayoutColumn(1.3, new[] { new WidgetPlacement("schedule", 1) }),
        new LayoutColumn(1, new[] { new WidgetPlacement("tasks", 1.05), new WidgetPlacement("notes", 1.1), new WidgetPlacement("scripture", 0.85) }),
    }, hiddenWidgetIds: new[] { "clock", "weather", "media", "calculator", "calendar", "sysmonitor", "countdown", "focus", "habits" });
}
