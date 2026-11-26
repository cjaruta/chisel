using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Layout;

namespace Chisel.App.Tests.Headless;

// The main window's widgets come from the layout: the host builds columns of cards from it,
// keeps every card inside the window, and rebuilds when the layout changes.
public class LayoutHostHeadlessTests
{
    private static LayoutHost Host(Window window) => window.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static string[][] Ids(LayoutHost host) => host.Frames.Select(c => c.Select(f => f.WidgetId).ToArray()).ToArray();

    private static string Heading(WidgetFrame frame) => frame.Widget.Title;

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    [AvaloniaFact]
    public void TheThreeDefaultWidgetsAppearWithTheirHeadingsInTheDefaultColumns()
    {
        var window = UiSnapshot.ShowMainWindow(layoutRepository: UiSnapshot.FactoryDefaultLayout());
        var host = Host(window);

        Assert.Equal(new[] { new[] { "schedule" }, new[] { "tasks", "notes" } }, Ids(host));
        Assert.Equal(new[] { "Today", "Tasks", "Notes" }, host.Frames.SelectMany(c => c).Select(Heading));

        var schedule = InWindow(host.Frames[0][0], window);
        var tasks = InWindow(host.Frames[1][0], window);
        Assert.True(schedule.Right < tasks.Left, "the schedule column is left of the lists");
        Assert.True(schedule.Width > tasks.Width, "the schedule column is the wider one");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(1000, 650)]
    [InlineData(1200, 760)]
    [InlineData(1904, 984)]
    public void NothingIsClipped(int width, int height)
    {
        var window = UiSnapshot.ShowMainWindow(width, height);
        var client = new Rect(window.ClientSize);
        var frames = Host(window).Frames.SelectMany(c => c).ToList();

        foreach (var frame in frames)
        {
            var bounds = InWindow(frame, window);
            Assert.True(client.Contains(bounds), $"{frame.WidgetId} at {bounds} is outside {client}");
            Assert.True(bounds.Height >= frame.MinHeight - 0.5, $"{frame.WidgetId} is shorter than its minimum");

            var heading = frame.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains("widgetHeading"));
            if (heading is not null)
            {
                Assert.True(bounds.Contains(InWindow(heading, window)), $"{frame.WidgetId} heading is cut off");
            }
            if (frame.View.BodyActions is { } actions)
            {
                Assert.True(bounds.Contains(InWindow(actions, window)), $"{frame.WidgetId} buttons are cut off");
            }
        }

        for (var i = 0; i < frames.Count; i++)
        {
            for (var j = i + 1; j < frames.Count; j++)
            {
                Assert.False(InWindow(frames[i], window).Intersects(InWindow(frames[j], window)),
                    $"{frames[i].WidgetId} overlaps {frames[j].WidgetId}");
            }
        }
        window.Close();
    }

    [AvaloniaFact]
    public void ChangingTheLayout_RebuildsTheColumnsLiveAndKeepsEachWidgetsView()
    {
        var window = UiSnapshot.ShowMainWindow();
        var host = Host(window);
        var scheduleView = host.Frames[0][0].View;
        var layout = ((MainViewModel)window.DataContext!).Layout;

        layout.Apply(new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("tasks"), new WidgetPlacement("scripture") }),
            new LayoutColumn(2, new[] { new WidgetPlacement("schedule") }),
            new LayoutColumn(1, new[] { new WidgetPlacement("notes") }),
        }));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new[] { new[] { "tasks", "scripture" }, new[] { "schedule" }, new[] { "notes" } }, Ids(host));
        Assert.Same(scheduleView, host.Frames[1][0].View);
        var middle = InWindow(host.Frames[1][0], window);
        Assert.True(middle.Width > InWindow(host.Frames[0][0], window).Width * 1.5, "the 2-weight column is about twice as wide");
        UiSnapshot.Render(window, "layout-three-columns");
        window.Close();
    }

    [AvaloniaFact]
    public void ColumnWidthsFollowTheirWeights()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        var host = Host(window);

        var left = InWindow(host.Frames[0][0], window).Width;
        var right = InWindow(host.Frames[1][0], window).Width;

        Assert.Equal(1.3, left / right, 1);
        window.Close();
    }

    // The schedule's pointer features still work inside its hosted card.
    [AvaloniaFact]
    public void HostedSchedule_HoverLineAndDragToCreateStillWork()
    {
        var window = UiSnapshot.ShowMainWindow();
        Named<ScrollViewer>(window, "ScheduleScroller").Offset = new Vector(0, 600);
        Dispatcher.UIThread.RunJobs();
        var blocks = Named<ItemsControl>(window, "ScheduleBlocks");
        Assert.NotNull(blocks.FindAncestorOfType<WidgetFrame>());
        Point At(double minute) => blocks.TranslatePoint(new Point(blocks.Bounds.Width / 2, minute), window)!.Value;

        window.MouseMove(At(12 * 60 + 37));
        Dispatcher.UIThread.RunJobs();
        Assert.True(Named<Control>(window, "HoverLine").IsVisible);
        Assert.Equal("12:30", Named<TextBlock>(window, "HoverLabel").Text);

        window.MouseDown(At(16 * 60), MouseButton.Left);
        window.MouseMove(At(16 * 60 + 40), RawInputModifiers.LeftMouseButton);
        window.MouseMove(At(17 * 60 + 15), RawInputModifiers.LeftMouseButton);
        window.MouseUp(At(17 * 60 + 15), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named<Border>(window, "BlockEditorPanel").IsVisible);
        Assert.Equal("16:00", Named<TextBox>(window, "BlockStartBox").Text);
        Assert.Equal("17:15", Named<TextBox>(window, "BlockEndBox").Text);
        window.Close();
    }
}
