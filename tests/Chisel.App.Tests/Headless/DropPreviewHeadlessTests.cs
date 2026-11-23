using System;
using System.Linq;
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

namespace Chisel.App.Tests.Headless;

// The drop preview while dragging: one translucent accent rectangle with a caption, one zone at a time, and a few
// pixels of hysteresis so a pointer resting on a boundary does not flicker.
public class DropPreviewHeadlessTests
{
    private static LayoutHost Host(Window window) => window.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static WidgetFrame Frame(Window window, string id) =>
        Host(window).Frames.SelectMany(c => c).Single(f => f.WidgetId == id);

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    private static Border Preview(Window window) =>
        window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "DropPreview");

    private static string Caption(Window window) =>
        window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "DropPreviewCaption")
            .GetVisualDescendants().OfType<TextBlock>().Single().Text!;

    private static Point Within(Window window, string id, double fraction)
    {
        var bounds = InWindow(Frame(window, id), window);
        return new Point(bounds.Center.X, bounds.Top + bounds.Height * fraction);
    }

    private static void MoveTo(Window window, Point point)
    {
        window.MouseMove(point, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
    }

    // Edit mode with the widget picked up and moved a little, so the drag has started.
    private static Window Dragging(string id)
    {
        var window = UiSnapshot.ShowMainWindow(1200, 760);
        ((MainViewModel)window.DataContext!).Layout.EnterEditCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var bar = InWindow(Frame(window, id).EditBar, window).Center;
        window.MouseMove(bar);
        window.MouseDown(bar, MouseButton.Left);
        MoveTo(window, new Point(bar.X + 10, bar.Y + 10));
        return window;
    }

    [AvaloniaTheory]
    [InlineData("tasks", 0.5, "Tab with Tasks")]
    [InlineData("tasks", 0.03, "Above Tasks")]
    [InlineData("scripture", 0.97, "Below Daily verse")]
    public void EachZone_ShowsTheOnePreview_WithItsCaption(string target, double fraction, string caption)
    {
        var window = Dragging("notes");

        MoveTo(window, Within(window, target, fraction));

        Assert.True(Preview(window).IsVisible);
        Assert.Equal(caption, Caption(window));
        Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("dropPreview"));
        window.Close();
    }

    [AvaloniaFact]
    public void AMergePreview_CoversTheWholeCard()
    {
        var window = Dragging("scripture");

        MoveTo(window, Within(window, "tasks", 0.5));

        var card = InWindow(Frame(window, "tasks"), window);
        var preview = InWindow(Preview(window), window);
        Assert.Equal(card.Left, preview.Left, 1.5);
        Assert.Equal(card.Top, preview.Top, 1.5);
        Assert.Equal(card.Width, preview.Width, 1.5);
        Assert.Equal(card.Height, preview.Height, 1.5);
        window.Close();
    }

    [AvaloniaFact]
    public void ANewColumnZone_ShowsTheNewColumnPreview_AndOnlyOnePreviewAtATime()
    {
        var window = Dragging("notes");

        MoveTo(window, Within(window, "tasks", 0.5));
        Assert.Equal("Tab with Tasks", Caption(window));
        MoveTo(window, new Point(6, 300));

        Assert.True(Preview(window).IsVisible);
        Assert.Equal("New column", Caption(window));
        Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("dropPreview"));
        window.Close();
    }

    [AvaloniaFact]
    public void TheHomeSlot_ShowsNoPreview()
    {
        var window = Dragging("notes");

        MoveTo(window, Within(window, "notes", 0.5));

        Assert.False(Preview(window).IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void ABoundaryJitterOfAFewPixels_DoesNotSwitchTheZone()
    {
        var window = Dragging("scripture");
        var card = InWindow(Frame(window, "tasks"), window);
        var x = card.Center.X;

        // Just inside the merge zone, then a pixel or two across the edge band: still the merge preview.
        MoveTo(window, new Point(x, card.Top + 13));
        Assert.Equal("Tab with Tasks", Caption(window));
        MoveTo(window, new Point(x, card.Top + 11));
        Assert.Equal("Tab with Tasks", Caption(window));
        MoveTo(window, new Point(x, card.Top + 13));
        Assert.Equal("Tab with Tasks", Caption(window));

        // Clearly across: it switches.
        MoveTo(window, new Point(x, card.Top + 5));
        Assert.Equal("Above Tasks", Caption(window));
        // And back across by a pixel or two does not flip it straight back.
        MoveTo(window, new Point(x, card.Top + 13));
        Assert.Equal("Tab with Tasks", Caption(window));
        MoveTo(window, new Point(x, card.Top + 11));
        Assert.Equal("Tab with Tasks", Caption(window));
        window.Close();
    }
}
