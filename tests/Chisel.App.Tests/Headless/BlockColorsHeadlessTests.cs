using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Models;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.Core.Schedule;

namespace Chisel.App.Tests.Headless;

// Blocks drawn in their palette color: tinted fill, solid left stripe, past ones dimmed,
// the current one outlined. The block editor picks the color with round swatches.
public class BlockColorsHeadlessTests
{
    // Now is 09:30 (UiSnapshot.SampleNow): three past blocks, "Focus" current, the rest later.
    private static readonly ScheduleBlock[] EveryColor =
    {
        new("Wake up", new TimeOnly(6, 30), new TimeOnly(7, 0), BlockColor.Amber),
        new("Breakfast", new TimeOnly(7, 0), new TimeOnly(8, 0), BlockColor.Rose),
        new("Commute", new TimeOnly(8, 0), new TimeOnly(9, 0), BlockColor.Gray),
        new("Focus", new TimeOnly(9, 0), new TimeOnly(10, 30), BlockColor.Slate),
        new("Email", new TimeOnly(10, 30), new TimeOnly(11, 30), BlockColor.Teal),
        new("Lunch", new TimeOnly(11, 30), new TimeOnly(12, 30), BlockColor.Terracotta),
        new("Workout", new TimeOnly(12, 30), new TimeOnly(13, 30), BlockColor.Sage),
        new("Read", new TimeOnly(13, 30), new TimeOnly(15, 0), BlockColor.Lavender),
    };

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static ScheduleViewModel Schedule(MainWindow window) => ((MainViewModel)window.DataContext!).Schedule;

    private static MainWindow Show()
    {
        var window = UiSnapshot.ShowMainWindow(1200, 1000, blocks: EveryColor);
        Named<ScrollViewer>(window, "ScheduleScroller").Offset = new Vector(0, 6 * 60);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Border Block(Window window, string title) =>
        window.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Classes.Contains("gridBlock"))
            .Single(b => b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Classes.Contains("blockTitle") && t.Text == title));

    private static Color BrushColor(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static Border Stripe(Border block) =>
        block.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("blockStripe"));

    [AvaloniaFact]
    public void EachBlock_IsFilledAndStripedInItsOwnColor()
    {
        var window = Show();

        foreach (var block in EveryColor)
        {
            var border = Block(window, block.Title);
            var swatch = BlockPalette.Get(block.Color);
            Assert.Equal(swatch.Fill, BrushColor(border.Background));
            Assert.Equal(swatch.Stripe, BrushColor(Stripe(border).Background));
        }

        UiSnapshot.Render(window, "block-colors");
        window.Close();
    }

    [AvaloniaFact]
    public void PastBlocks_AreDimmedAndTheCurrentBlockIsOutlinedNotRecolored()
    {
        var window = Show();

        var past = Block(window, "Commute");
        var current = Block(window, "Focus");
        var later = Block(window, "Email");

        Assert.Contains("past", past.Classes);
        Assert.True(past.Opacity < 1);
        Assert.Contains("current", current.Classes);
        Assert.Equal(1, current.Opacity);
        Assert.Equal(BlockPalette.Get(BlockColor.Slate).Fill, BrushColor(current.Background));
        Assert.Equal(Color.Parse("#F2F4F8"), BrushColor(current.BorderBrush));
        Assert.True(current.BorderThickness.Left > 0);
        Assert.NotEqual(default, current.BoxShadow);
        Assert.Equal(Colors.Transparent, BrushColor(later.BorderBrush));
        window.Close();
    }

    [AvaloniaFact]
    public void Editor_ShowsTheBlocksColorSelectedAndClickingASwatchColorsThePendingBlock()
    {
        var window = Show();
        var blocks = Named<ItemsControl>(window, "ScheduleBlocks");
        Point At(double minute) => blocks.TranslatePoint(new Point(blocks.Bounds.Width / 2, minute), window)!.Value;

        // Drag 16:00 to 17:30 on empty space to get a pending block, then pick a swatch with the pointer.
        window.MouseMove(At(16 * 60));
        window.MouseDown(At(16 * 60), MouseButton.Left);
        window.MouseMove(At(16 * 60 + 45), RawInputModifiers.LeftMouseButton);
        window.MouseMove(At(17 * 60 + 30), RawInputModifiers.LeftMouseButton);
        window.MouseUp(At(17 * 60 + 30), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var swatches = Named<ItemsControl>(window, "BlockSwatches").GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("swatch")).ToList();
        Assert.Equal(8, swatches.Count);
        Assert.Equal(new[] { BlockColor.Default }, swatches.Where(b => b.Classes.Contains("selected"))
            .Select(b => ((ColorSwatchOption)b.DataContext!).Key));

        var sage = swatches.Single(b => ((ColorSwatchOption)b.DataContext!).Key == BlockColor.Sage);
        var center = sage.TranslatePoint(new Point(sage.Bounds.Width / 2, sage.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(BlockColor.Sage, Schedule(window).BlockEditor.ColorKey);
        Assert.Contains("selected", sage.Classes);
        Assert.Equal(BlockPalette.Get(BlockColor.Sage).Fill, BrushColor(Named<Border>(window, "PendingFill").Background));
        Assert.Equal(BlockPalette.Get(BlockColor.Sage).Stripe, BrushColor(Named<Border>(window, "PendingStripe").Background));
        UiSnapshot.Render(window, "block-colors-editor");
        window.Close();
    }

    [AvaloniaFact]
    public void EditingABlock_ShowsItsColorSelected()
    {
        var window = Show();
        Schedule(window).EditBlockCommand.Execute(Schedule(window).GridBlocks.Single(b => b.Title == "Read"));
        Dispatcher.UIThread.RunJobs();

        var selected = Named<ItemsControl>(window, "BlockSwatches").GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("swatch") && b.Classes.Contains("selected"))
            .Select(b => ((ColorSwatchOption)b.DataContext!).Key);
        Assert.Equal(new[] { BlockColor.Lavender }, selected);
        window.Close();
    }
}
