using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.Tests.Support;
using Chisel.App.ViewModels;
using Chisel.App.Views;
using Chisel.App.Views.Widgets;
using Chisel.Core.Layout;
using Chisel.Core.Pictures;
using Xunit;

namespace Chisel.App.Tests.Headless;

// Picture frames on the real window: what a frame shows in each mode, what the drop previews say, what a drop does
// (placeholder, then the picture, or a message), the adjust state, clicks and keys, and when a frame goes quiet.
public class PictureFrameHeadlessTests
{
    private const string A = "pictureframe:aaa111";
    private const string B = "pictureframe:bbb222";

    private static void Settle() => Dispatcher.UIThread.RunJobs();

    private static void WaitUntil(Func<bool> condition, string what)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            Settle();
            Thread.Sleep(10);
        }
        Settle();
        Assert.True(condition(), "timed out waiting for " + what);
    }

    private static LayoutHost Host(Window window) => window.GetVisualDescendants().OfType<LayoutHost>().Single();

    private static WidgetFrame Frame(Window window, string id) =>
        Host(window).Frames.SelectMany(c => c).Single(f => f.MemberIds.Contains(id));

    private static MainViewModel Main(Window window) => (MainViewModel)window.DataContext!;

    private static PictureFrameView View(Window window, string id)
    {
        var frame = Frame(window, id);
        return (PictureFrameView)frame.Views[frame.MemberIds.ToList().IndexOf(id)];
    }

    private static Rect InWindow(Visual visual, Window window) =>
        new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    private static string Caption(Window window) =>
        window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "DropPreviewCaption")
            .GetVisualDescendants().OfType<TextBlock>().Single().Text!;

    private static Border Preview(Window window) =>
        window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "DropPreview");

    private static Point ToHost(Window window, Point inWindow) => window.TranslatePoint(inWindow, Host(window))!.Value;

    private static Point Within(Window window, string id, double fraction)
    {
        var bounds = InWindow(Frame(window, id), window);
        return new Point(bounds.Center.X, bounds.Top + bounds.Height * fraction);
    }

    private static Color Pixel(MainWindow window, Point point)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();
        var value = Marshal.ReadInt32(buffer.Address, (int)point.Y * buffer.RowBytes + (int)point.X * 4);
        var (low, high) = ((byte)value, (byte)(value >> 16));
        return buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888
            ? Color.FromArgb((byte)(value >> 24), high, (byte)(value >> 8), low)
            : Color.FromArgb((byte)(value >> 24), low, (byte)(value >> 8), high);
    }

    // A window whose second column holds Tasks, one or two picture frames, then Notes and Links.
    private static MainWindow Open(PictureKit kit, params string[] frames) => Open(kit, 1200, 760, frames);

    private static MainWindow Open(PictureKit kit, double width, double height, params string[] frames)
    {
        var window = UiSnapshot.ShowMainWindow(width, height, pictures: kit);
        var main = Main(window);
        var layout = main.Layout.Layout;
        foreach (var frame in frames)
        {
            layout = layout.AddWidget(frame, 1, 1, 1);
        }
        main.Layout.Apply(layout);
        Settle();
        return window;
    }

    private static void Fill(MainWindow window, string frame, params PictureAsset[] assets)
    {
        Main(window).Layout.UpdatePictures(p => p.AddPictures(frame, assets.Select(a => a.Id)));
        Settle();
    }

    private static void Options(MainWindow window, string frame, Func<FrameOptions, FrameOptions> change) =>
        Main(window).Layout.UpdatePictures(p => p.SetOptions(frame, change(p.Get(frame).Options)));

    private static void Loaded(MainWindow window, string id) =>
        WaitUntil(() => View(window, id).Canvas.Current is not null || View(window, id).IsPlayingGif, "the picture to load");

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    private static Color CardColor(Window window) =>
        ((ISolidColorBrush)Chisel.App.Theming.ThemeService.Brush(window, "CardBackgroundBrush")).Color;

    // ----- display

    [AvaloniaFact]
    public void Fill_CoversTheCard_AndFit_LeavesTheCardColorAroundAWidePicture()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);
        Fill(window, A, kit.Add(SampleImages.Portrait()));
        Loaded(window, A);
        var canvas = View(window, A).Canvas;
        var bounds = InWindow(canvas, window);
        var topCenter = new Point(bounds.Left + 6, bounds.Center.Y);
        var card = CardColor(window);

        // Fill: a tall picture in a wide card reaches the card's left edge.
        Assert.True(bounds.Width > bounds.Height, "the card is wider than the picture");
        Assert.NotEqual(card, Pixel(window, topCenter));

        Options(window, A, o => o with { Fit = FitMode.Fit });
        Settle();
        Assert.Equal(card, Pixel(window, topCenter));

        Options(window, A, o => o with { BlurFill = true });
        Settle();
        Assert.NotEqual(card, Pixel(window, topCenter));
        window.Close();
    }

    [AvaloniaFact]
    public void TheHeader_IsHiddenByDefault_AndThePictureFillsTheCard()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);
        Fill(window, A, kit.Add(SampleImages.Landscape()));
        Loaded(window, A);

        var frame = Frame(window, A);
        Assert.False(frame.HeaderShown);
        var card = InWindow(frame, window);
        var canvas = InWindow(View(window, A).Canvas, window);
        // The picture fills the card inside its one pixel border.
        Assert.InRange(card.Width - canvas.Width, 0, 4);
        Assert.InRange(card.Height - canvas.Height, 0, 4);
        window.Close();
    }

    [AvaloniaFact]
    public void AnEmptyFrame_SaysHowToFillIt_AndAHugePictureIsDecodedSmall()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);

        Assert.True(View(window, A).GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PictureHint").IsVisible);

        Fill(window, A, kit.Add(SampleImages.Huge()));
        Loaded(window, A);

        var bitmap = View(window, A).Canvas.Current!.Bitmap;
        Assert.True(Math.Max(bitmap.PixelSize.Width, bitmap.PixelSize.Height) <= 2560);
        Assert.False(View(window, A).GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PictureHint").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void ATinyAndAPortraitPicture_ShowWithoutTrouble()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A, B);
        Fill(window, A, kit.Add(SampleImages.Tiny(), "tiny.png"));
        Fill(window, B, kit.Add(SampleImages.Portrait(), "portrait.png"));
        Loaded(window, A);
        Loaded(window, B);

        Assert.Equal(8, View(window, A).Canvas.Current!.Bitmap.PixelSize.Width);
        Assert.Equal(270, View(window, B).Canvas.Current!.Bitmap.PixelSize.Width);
        window.Close();
    }

    // ----- clicks and keys

    private static MainWindow WithPictures(PictureKit kit, int count, out Point right, out Point left, FrameOptions? options = null)
    {
        var window = Open(kit, A);
        Fill(window, A, Enumerable.Range(0, count).Select(i => kit.Add(SampleImages.Png(64 + i, 48, Colors.SteelBlue, Colors.Orange), $"p{i}.png")).ToArray());
        if (options is not null)
        {
            Options(window, A, _ => options);
        }
        Loaded(window, A);
        var bounds = InWindow(View(window, A), window);
        right = new Point(bounds.Left + bounds.Width * 0.75, bounds.Center.Y);
        left = new Point(bounds.Left + bounds.Width * 0.25, bounds.Center.Y);
        return window;
    }

    [AvaloniaFact]
    public void ClickingTheRightHalf_ShowsTheNextPicture_TheLeftHalfThePrevious_AndTheyWrap()
    {
        using var kit = new PictureKit();
        var window = WithPictures(kit, 3, out var right, out var left);
        var vm = View(window, A).Frame!;

        Click(window, right);
        Assert.Equal(1, vm.CurrentIndex);
        Click(window, left);
        Click(window, left);
        Assert.Equal(2, vm.CurrentIndex);
        Click(window, right);
        Assert.Equal(0, vm.CurrentIndex);
        window.Close();
    }

    [AvaloniaFact]
    public void ADrag_IsNotAClick_AndAFrameWithOnePictureIgnoresClicks()
    {
        using var kit = new PictureKit();
        var window = WithPictures(kit, 3, out var right, out _);
        var vm = View(window, A).Frame!;

        window.MouseMove(right);
        window.MouseDown(right, MouseButton.Left);
        window.MouseMove(new Point(right.X + 20, right.Y));
        window.MouseUp(new Point(right.X + 20, right.Y), MouseButton.Left);
        Settle();
        Assert.Equal(0, vm.CurrentIndex);
        window.Close();

        using var single = new PictureKit();
        var other = WithPictures(single, 1, out var r, out _);
        Click(other, r);
        Assert.Equal(0, View(other, A).Frame!.CurrentIndex);
        other.Close();
    }

    [AvaloniaFact]
    public void TheArrowKeys_WorkWhenTheFrameHasFocus_AndTheFrameHasAnAccessibleName()
    {
        using var kit = new PictureKit();
        var window = WithPictures(kit, 3, out var right, out _);
        var view = View(window, A);
        view.Focus();

        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        Settle();
        Assert.Equal(1, view.Frame!.CurrentIndex);
        window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
        window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
        Settle();
        Assert.Equal(2, view.Frame!.CurrentIndex);
        Assert.Equal("Picture frame", Avalonia.Automation.AutomationProperties.GetName(view));
        Assert.Contains("arrow", Avalonia.Automation.AutomationProperties.GetHelpText(view), StringComparison.OrdinalIgnoreCase);
        window.Close();
    }

    [AvaloniaFact]
    public void TheFade_RunsOnAChange_AndNeverWithTransitionNone()
    {
        using var kit = new PictureKit();
        var window = WithPictures(kit, 3, out var right, out _);
        var view = View(window, A);

        Click(window, right);
        WaitUntil(() => view.IsFading || view.Canvas.Current is not null, "the next picture");
        Assert.True(view.IsFading);
        view.CompleteFade();
        Assert.False(view.IsFading);

        Options(window, A, o => o with { Transition = SlideshowTransition.None });
        Click(window, right);
        WaitUntil(() => view.Frame!.CurrentIndex == 2, "the third picture");
        Assert.False(view.IsFading);
        window.Close();
    }

    [AvaloniaFact]
    public void OnlyThePictureShownAndTheNextAreKeptDecoded()
    {
        using var kit = new PictureKit();
        var window = WithPictures(kit, 6, out var right, out _);
        var view = View(window, A);

        for (var i = 0; i < 5; i++)
        {
            Click(window, right);
            WaitUntil(() => view.Canvas.Current is not null, "a picture");
            view.CompleteFade();
            Assert.True(view.DecodedCount <= 2, $"at most 2 decoded, got {view.DecodedCount}");
        }
        window.Close();
    }

    // ----- idle

    [AvaloniaFact]
    public void ACyclingFrame_StopsInEditMode_InAHiddenTab_AndWhenTheWindowIsMinimized()
    {
        using var kit = new PictureKit();
        var window = WithPictures(kit, 3, out _, out _, new FrameOptions(Mode: SlideshowMode.Cycle, IntervalSeconds: 10));
        var main = Main(window);
        var vm = View(window, A).Frame!;
        main.Tick();

        kit.Time.Advance(TimeSpan.FromSeconds(11));
        main.Tick();
        Assert.Equal(1, vm.CurrentIndex);

        main.Layout.EnterEditCommand.Execute(null);
        Assert.False(vm.IsRunning);
        kit.Time.Advance(TimeSpan.FromMinutes(5));
        main.Tick();
        Assert.Equal(1, vm.CurrentIndex);
        main.Layout.CancelCommand.Execute(null);
        Settle();

        window.WindowState = WindowState.Minimized;
        Assert.False(vm.IsRunning);
        kit.Time.Advance(TimeSpan.FromMinutes(5));
        main.Tick();
        Assert.Equal(1, vm.CurrentIndex);
        window.WindowState = WindowState.Normal;
        Assert.True(vm.IsRunning);
        window.Close();
    }

    [AvaloniaFact]
    public void AFrameInAHiddenTab_IsQuiet_AndWorksInSplitPanes()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);
        Fill(window, A, kit.Add(SampleImages.Landscape()), kit.Add(SampleImages.Portrait(), "p.png"));
        Options(window, A, o => o with { Mode = SlideshowMode.Cycle, IntervalSeconds = 10 });
        var main = Main(window);
        main.Layout.Apply(main.Layout.Layout.Combine(A, "notes"));
        Settle();
        main.Layout.SelectTab("notes");
        Settle();
        var vm = main.PictureFrames.For(A);

        Assert.False(vm.IsRunning);
        main.Tick();
        kit.Time.Advance(TimeSpan.FromMinutes(5));
        main.Tick();
        Assert.Equal(0, vm.CurrentIndex);

        main.Layout.SelectTab(A);
        Settle();
        Assert.True(vm.IsRunning);

        var tabs = main.Layout.Layout.Columns.SelectMany(c => c.Widgets).Single(w => w.IsGroup);
        main.Layout.SetViewMode(tabs.WidgetId, GroupViewMode.Split, 700);
        Settle();
        Assert.True(Frame(window, A).IsSplitShown);
        Assert.True(vm.IsRunning);
        Assert.True(View(window, A).IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void AnAnimatedGif_PlaysOnlyWhileTheFrameRuns()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);
        Fill(window, A, kit.Add(SampleImages.Gif(), "anim.gif"));
        var view = View(window, A);
        WaitUntil(() => view.IsPlayingGif, "the GIF to start");
        var main = Main(window);

        main.Layout.EnterEditCommand.Execute(null);
        WaitUntil(() => !view.IsPlayingGif && view.Canvas.Current is not null, "the GIF to pause on its first frame");
        main.Layout.CancelCommand.Execute(null);
        WaitUntil(() => view.IsPlayingGif, "the GIF to play again");

        window.WindowState = WindowState.Minimized;
        WaitUntil(() => !view.IsPlayingGif, "the GIF to pause when minimized");
        window.WindowState = WindowState.Normal;
        WaitUntil(() => view.IsPlayingGif, "the GIF to resume");

        main.Layout.Apply(main.Layout.Layout.Combine(A, "notes"));
        main.Layout.SelectTab("notes");
        WaitUntil(() => !view.IsPlayingGif, "the GIF to pause in a hidden tab");
        window.Close();
    }

    // ----- drop previews

    [AvaloniaTheory]
    [InlineData(0.5, "Add to Picture frame")]
    [InlineData(0.03, "Above Picture frame")]
    [InlineData(0.97, "Above Notes")]
    public void OverAPictureFrame_TheCaptionsSayAddToAboveAndBelow(double fraction, string caption)
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);
        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();

        Assert.True(Host(window).ExternalDragOver(ToHost(window, Within(window, A, fraction))));
        Settle();

        Assert.True(Preview(window).IsVisible);
        Assert.Equal(caption, Caption(window));
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("tasks", 0.5, "Tab with Tasks")]
    [InlineData("tasks", 0.03, "Above Tasks")]
    [InlineData("links", 0.97, "Below Quick links")]
    public void OverAnotherWidget_TheCaptionsSayTabWithAboveAndBelow(string id, double fraction, string caption)
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);
        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();

        Assert.True(Host(window).ExternalDragOver(ToHost(window, Within(window, id, fraction))));
        Settle();

        Assert.Equal(caption, Caption(window));
        window.Close();
    }

    [AvaloniaFact]
    public void TheGutterAtTheEdge_SaysNewColumn_AndLeavingHidesThePreview()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);
        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();

        Host(window).ExternalDragOver(ToHost(window, new Point(6, 300)));
        Settle();
        Assert.Equal("New column", Caption(window));
        Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("dropPreview") && b.IsVisible);

        Host(window).ExternalDragLeave();
        Settle();
        Assert.False(Preview(window).IsVisible);
        Assert.False(Main(window).Layout.IsDraggingPictures);
        window.Close();
    }

    [AvaloniaFact]
    public void OnAGroupWhoseShownWidgetIsAPictureFrame_TheCaptionSaysAddTo()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);
        var main = Main(window);
        main.Layout.Apply(main.Layout.Layout.Combine("notes", A));
        main.Layout.SelectTab(A);
        main.Layout.EnterEditCommand.Execute(null);
        Settle();

        Host(window).ExternalDragOver(ToHost(window, Within(window, A, 0.5)));
        Settle();

        Assert.Equal("Add to Picture frame", Caption(window));
        window.Close();
    }

    [AvaloniaFact]
    public void OutsideEditMode_ADropIsRefused_AndNothingChanges()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);
        var before = Main(window).Layout.Layout.Describe();

        Assert.False(Host(window).ExternalDragOver(ToHost(window, Within(window, A, 0.5))));
        Assert.Null(Host(window).ExternalDrop(ToHost(window, Within(window, A, 0.5)), 1));
        Settle();

        Assert.False(Preview(window).IsVisible);
        Assert.Equal(before, Main(window).Layout.Layout.Describe());
        Assert.False(Main(window).Layout.IsDraggingWidget);
        window.Close();
    }

    // ----- drops

    private static Task Drop(MainWindow window, Point point, params PictureSource[] sources)
    {
        var main = Main(window);
        Assert.True(Host(window).ExternalDragOver(ToHost(window, point)));
        var placement = Host(window).ExternalDrop(ToHost(window, point), main.PictureFrames.FramesFor(sources.Length))!;
        Settle();
        return main.PictureFrames.DropAsync(placement, sources);
    }

    [AvaloniaFact]
    public void ADrop_ShowsAPlaceholderAtOnce_ThenFillsItIn()
    {
        using var kit = new PictureKit();
        var window = Open(kit);
        var main = Main(window);
        main.Layout.EnterEditCommand.Execute(null);
        Settle();
        var gate = new TaskCompletionSource<DownloadResult>();
        kit.Downloader.Held["https://example.com/a.png"] = gate.Task;

        var running = Drop(window, Within(window, "tasks", 0.97), new UrlSource("https://example.com/a.png"));

        // The frame is on the dashboard right away, loading.
        var id = main.Layout.Layout.WidgetIds.Single(i => i.StartsWith(WidgetRegistry.PictureFrame));
        Assert.Equal(1, main.PictureFrames.For(id).Pending);
        Assert.Equal(0, View(window, id).Frame!.Count);
        Assert.False(View(window, id).GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PictureHint").IsVisible);

        gate.SetResult(new DownloadResult(SampleImages.Landscape(), FileName: "a.png"));
        WaitUntil(() => running.IsCompleted, "the drop to finish");
        Assert.Equal(0, main.PictureFrames.For(id).Pending);
        Loaded(window, id);
        Assert.Single(main.Layout.Pictures.Get(id).Pictures);
        window.Close();
    }

    [AvaloniaFact]
    public void ADropOfAPicture_MakesAFrameThatShowsIt()
    {
        using var kit = new PictureKit();
        var window = Open(kit);
        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();

        var task = Drop(window, Within(window, "tasks", 0.97), new BytesSource(SampleImages.Landscape(), "cat.png"));
        WaitUntil(() => task.IsCompleted, "the drop to finish");

        var id = Main(window).Layout.Layout.WidgetIds.Single(i => i.StartsWith(WidgetRegistry.PictureFrame));
        Loaded(window, id);
        Assert.Single(Main(window).Layout.Pictures.Get(id).Pictures);
        Assert.Equal(new[] { "tasks", id, "notes", "links" }, Main(window).Layout.Layout.Columns[1].Widgets.Select(w => w.WidgetId));
        window.Close();
    }

    [AvaloniaFact]
    public void ADropThatFails_ShowsAMessage_AndLeavesNoEmptyFrame()
    {
        using var kit = new PictureKit();
        var window = Open(kit);
        var main = Main(window);
        main.Layout.EnterEditCommand.Execute(null);
        Settle();

        var task = Drop(window, Within(window, "tasks", 0.97), new UrlSource("https://example.com/missing.png"));
        WaitUntil(() => task.IsCompleted, "the drop to finish");

        Assert.DoesNotContain(main.Layout.Layout.WidgetIds, i => i.StartsWith(WidgetRegistry.PictureFrame));
        Assert.Contains("Couldn't download", main.PictureFrames.Notice);
        var notice = window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PictureNotice");
        Assert.True(notice.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void ADropOnAPictureFrame_AddsToItsSlideshow()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);
        Fill(window, A, kit.Add(SampleImages.Landscape()));
        Main(window).Layout.EnterEditCommand.Execute(null);
        Settle();

        var task = Drop(window, Within(window, A, 0.5), new BytesSource(SampleImages.Portrait(), "two.png"));
        WaitUntil(() => task.IsCompleted, "the drop to finish");

        Assert.Equal(2, Main(window).Layout.Pictures.Get(A).Pictures.Count);
        Assert.Single(Main(window).Layout.Layout.WidgetIds, i => i.StartsWith(WidgetRegistry.PictureFrame));
        window.Close();
    }

    [AvaloniaFact]
    public void Cancel_AfterADrop_PutsTheDashboardBack_AndDoneKeepsIt()
    {
        using var kit = new PictureKit();
        var window = Open(kit);
        var main = Main(window);
        var before = main.Layout.Layout.Describe();
        main.Layout.EnterEditCommand.Execute(null);
        Settle();
        var task = Drop(window, Within(window, "tasks", 0.97), new BytesSource(SampleImages.Landscape(), "cat.png"));
        WaitUntil(() => task.IsCompleted, "the drop to finish");

        main.Layout.CancelCommand.Execute(null);
        Settle();
        Assert.Equal(before, main.Layout.Layout.Describe());
        Assert.Empty(main.Layout.Pictures.FrameIds);

        main.Layout.EnterEditCommand.Execute(null);
        var again = Drop(window, Within(window, "tasks", 0.97), new BytesSource(SampleImages.Portrait(), "dog.png"));
        WaitUntil(() => again.IsCompleted, "the drop to finish");
        main.Layout.DoneCommand.Execute(null);
        Settle();
        Assert.Single(main.Layout.Pictures.FrameIds);
        Assert.Single(kit.Repository.Assets());
        window.Close();
    }

    // ----- adjust

    [AvaloniaFact]
    public void TheAdjustBadge_ShowsOnlyOnAPictureFrameInEditMode_AndAdjustingPansZoomsAndResets()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A);
        Fill(window, A, kit.Add(SampleImages.Landscape()));
        Loaded(window, A);
        var main = Main(window);
        var frame = Frame(window, A);

        Assert.False(frame.AdjustButton.IsVisible);
        main.Layout.EnterEditCommand.Execute(null);
        Settle();
        Assert.True(frame.AdjustButton.IsVisible);
        Assert.False(Frame(window, "notes").AdjustButton.IsVisible);

        frame.AdjustButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Settle();
        Assert.Equal(A, main.Layout.AdjustingFrameId);
        Assert.True(frame.IsAdjusting);
        Assert.False(frame.EditBar.IsVisible);
        Assert.True(window.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PictureAdjustBar").IsVisible);

        // Drag to pan: a wide card shows a band of the picture, so it can move up and down.
        var center = InWindow(View(window, A), window).Center;
        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Left);
        window.MouseMove(new Point(center.X, center.Y - 20), RawInputModifiers.LeftMouseButton);
        window.MouseUp(new Point(center.X, center.Y - 20), MouseButton.Left);
        Settle();
        Assert.True(main.Layout.Pictures.Get(A).Pictures[0].FocalY > 0.5);

        window.MouseWheel(center, new Vector(0, 3));
        Settle();
        Assert.True(main.Layout.Pictures.Get(A).Pictures[0].Zoom > 1.2);

        window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PictureAdjustReset").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Settle();
        var reset = main.Layout.Pictures.Get(A).Pictures[0];
        Assert.Equal((0.5, 0.5, 1.0), (reset.FocalX, reset.FocalY, reset.Zoom));

        window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PictureAdjustDone").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Settle();
        Assert.Null(main.Layout.AdjustingFrameId);
        Assert.False(frame.IsAdjusting);
        Assert.True(frame.EditBar.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void Cancel_DropsWhatAdjustingChanged_AndAGifCannotBeAdjusted()
    {
        using var kit = new PictureKit();
        var window = Open(kit, A, B);
        Fill(window, A, kit.Add(SampleImages.Landscape()));
        Fill(window, B, kit.Add(SampleImages.Gif(), "anim.gif"));
        Loaded(window, A);
        Loaded(window, B);
        var main = Main(window);
        main.Layout.EnterEditCommand.Execute(null);
        Settle();

        Assert.False(main.Layout.BeginAdjust(B));
        Assert.True(main.Layout.BeginAdjust(A));
        main.PictureFrames.For(A).AdjustZoom(10);
        Assert.True(main.Layout.Pictures.Get(A).Pictures[0].Zoom > 1);

        main.Layout.CancelCommand.Execute(null);
        Settle();

        Assert.Equal(1, main.Layout.Pictures.Get(A).Pictures[0].Zoom);
        Assert.Null(main.Layout.AdjustingFrameId);
        window.Close();
    }
}
