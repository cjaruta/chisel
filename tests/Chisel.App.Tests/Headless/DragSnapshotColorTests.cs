using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.App.Views;

namespace Chisel.App.Tests.Headless;

// The floating snapshot is the card as it looks at rest: same average color at pick-up and for the first 300 ms.
public class DragSnapshotColorTests
{
    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static Point Center(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    private static (double R, double G, double B) Average(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var rowBytes = size.Width * 4;
        var bytes = new byte[rowBytes * size.Height];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(bytes, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), bytes.Length, rowBytes);
        }
        finally
        {
            handle.Free();
        }
        double r = 0, g = 0, b = 0;
        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var i = y * rowBytes + x * 4;
                b += bytes[i];
                g += bytes[i + 1];
                r += bytes[i + 2];
            }
        }
        var n = (double)size.Width * size.Height;
        return (r / n, g / n, b / n);
    }

    private static RenderTargetBitmap Render(Control control)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize((int)control.Bounds.Width, (int)control.Bounds.Height));
        bitmap.Render(control);
        return bitmap;
    }

    private static void Save(Bitmap bitmap, string name)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "ui");
        Directory.CreateDirectory(dir);
        bitmap.Save(Path.Combine(dir, name + ".png"));
    }

    [AvaloniaFact]
    public async Task PickUp_TurnsTheSlotIntoAGapAtOnce_UnderAnOpaqueSnapshot()
    {
        var window = UiSnapshot.ShowMainWindow();
        var c = Center(Named<Button>(window, "CustomizeLayoutButton"), window);
        window.MouseMove(c);
        window.MouseDown(c, MouseButton.Left);
        window.MouseUp(c, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        var host = window.GetVisualDescendants().OfType<LayoutHost>().Single();
        var frame = host.Frames.SelectMany(f => f).Single(f => f.WidgetId == "links");
        var floating = Named<Border>(window, "DragFloat");
        await Task.Delay(400);
        for (var round = 0; round < 2; round++)
        {
            var from = Center(frame.EditBar, window);
            window.MouseMove(from);
            window.MouseDown(from, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(new Point(from.X + 20, from.Y - 20), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, floating.Opacity);
            Assert.Equal(0, frame.Content!.Opacity);
            Assert.Equal(Avalonia.Media.Color.Parse("#14808080"), Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(frame.Background).Color);

            window.MouseUp(from, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(600);
            Dispatcher.UIThread.RunJobs();
        }
        window.Close();
    }

    [AvaloniaFact]
    public async Task Snapshot_MatchesTheCard_AtPickUp_AndStaysTheSame()
    {
        var window = UiSnapshot.ShowMainWindow();
        var customize = Named<Button>(window, "CustomizeLayoutButton");
        var c = Center(customize, window);
        window.MouseMove(c);
        window.MouseDown(c, MouseButton.Left);
        window.MouseUp(c, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var host = window.GetVisualDescendants().OfType<LayoutHost>().Single();
        var frame = host.Frames.SelectMany(f => f).Single(f => f.WidgetId == "links");
        await Task.Delay(400); // let edit mode settle
        Dispatcher.UIThread.RunJobs();
        using var card = Render(frame);
        Save(card, "snapshot-card-at-rest");
        var expected = Average(card);

        var from = Center(frame.EditBar, window);
        window.MouseMove(from);
        window.MouseDown(from, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(new Point(from.X + 20, from.Y - 20), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        var floating = Named<Border>(window, "DragFloat");
        var image = Assert.IsType<Image>(floating.Child);
        var pickUp = Assert.IsAssignableFrom<Bitmap>(image.Source);
        Save(pickUp, "snapshot-pickup");
        var atPickUp = Average(pickUp);

        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();
        var later = Assert.IsAssignableFrom<Bitmap>(Assert.IsType<Image>(floating.Child).Source);
        Save(later, "snapshot-300ms");
        var afterwards = Average(later);

        Assert.InRange(Math.Abs(atPickUp.R - expected.R) + Math.Abs(atPickUp.G - expected.G) + Math.Abs(atPickUp.B - expected.B), 0, 6);
        Assert.InRange(Math.Abs(afterwards.R - atPickUp.R) + Math.Abs(afterwards.G - atPickUp.G) + Math.Abs(afterwards.B - atPickUp.B), 0, 1);
        window.Close();
    }
}
