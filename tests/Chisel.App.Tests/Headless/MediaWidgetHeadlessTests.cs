using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.Core.Media;

namespace Chisel.App.Tests.Headless;

public class MediaWidgetHeadlessTests
{
    // Opens the window with only the media widget added; weight is its share of the first column's height.
    private static (Chisel.App.Views.MainWindow Window, MainViewModel Main, FakeMediaService Service) WindowWithMedia(
        MediaSessionInfo? info, double weight = 1)
    {
        var service = new FakeMediaService { Current = info };
        var window = UiSnapshot.ShowMainWindow(media: service);
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Media, 0, layout.Columns[0].Widgets.Count, weight));
        Dispatcher.UIThread.RunJobs();
        return (window, main, service);
    }

    private static T? Named<T>(Chisel.App.Views.MainWindow w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);

    [AvaloniaFact]
    public void Media_IsInTheTray_CanBeAdded_AndDoesNotOpenTheTrayByItself()
    {
        var window = UiSnapshot.ShowMainWindow();
        var main = (MainViewModel)window.DataContext!;
        main.CustomizeLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(main.Layout.TrayWidgets, w => w.Id == WidgetRegistry.Media);
        Assert.Empty(window.GetVisualDescendants().OfType<MediaWidgetView>());

        main.Layout.AddWidgetFromTray(WidgetRegistry.Media);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(window.GetVisualDescendants().OfType<MediaWidgetView>());
        Assert.True(main.Media.IsActive);
    }

    [AvaloniaFact]
    public void Playing_ShowsTheDetailsAndAPauseButton()
    {
        var (window, _, _) = WindowWithMedia(FakeMediaService.Sample() with { AlbumArt = Chisel.App.Dev.SampleArt.Png() }, weight: 1.6);

        Assert.Equal("Sample Song", Named<TextBlock>(window, "MediaTitle")!.Text);
        Assert.Equal("Test Artist", Named<TextBlock>(window, "MediaArtist")!.Text);
        Assert.Equal("Sample Album", Named<TextBlock>(window, "MediaAlbum")!.Text);
        Assert.Equal("Spotify", Named<TextBlock>(window, "MediaSource")!.Text);
        Assert.Equal("1:23", Named<TextBlock>(window, "MediaElapsed")!.Text);
        Assert.Equal("3:35", Named<TextBlock>(window, "MediaDuration")!.Text);
        Assert.NotNull(Named<Image>(window, "MediaArt")!.Source);
        Assert.Equal("Pause", Named<Button>(window, "MediaPlayPause")!.Tag);
        Assert.Null(Named<TextBlock>(window, "MediaNothing"));
    }

    [AvaloniaFact]
    public void Paused_ShowsAPlayButton()
    {
        var (window, _, _) = WindowWithMedia(FakeMediaService.Sample(MediaPlaybackState.Paused));

        Assert.Equal("Play", Named<Button>(window, "MediaPlayPause")!.Tag);
    }

    [AvaloniaFact]
    public void NothingPlaying_ShowsAQuietMessage_AndNoControls()
    {
        var (window, _, _) = WindowWithMedia(null);

        Assert.Equal("Nothing playing", Named<TextBlock>(window, "MediaNothing")!.Text);
        Assert.Null(Named<TextBlock>(window, "MediaTitle"));
        Assert.Null(Named<Button>(window, "MediaPlayPause"));
    }

    [AvaloniaFact]
    public void ControlsTheSessionDoesNotOffer_AreDisabled()
    {
        var (window, _, _) = WindowWithMedia(FakeMediaService.Sample() with { Controls = MediaControls.Pause | MediaControls.Next });

        Assert.False(Named<Button>(window, "MediaPrevious")!.IsEffectivelyEnabled);
        Assert.True(Named<Button>(window, "MediaPlayPause")!.IsEffectivelyEnabled);
        Assert.True(Named<Button>(window, "MediaNext")!.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void ClickingTheButtons_CallsTheService()
    {
        var (window, _, service) = WindowWithMedia(FakeMediaService.Sample());

        Named<Button>(window, "MediaNext")!.Command!.Execute(null);
        Named<Button>(window, "MediaPlayPause")!.Command!.Execute(null);

        Assert.Equal(new[] { "Next", "Pause" }, service.Calls);
    }

    [AvaloniaFact]
    public void TheProgressBar_FillsInProportion()
    {
        var (window, _, _) = WindowWithMedia(FakeMediaService.Sample());   // 83 of 215 seconds

        var fill = Named<Border>(window, "MediaProgressFill")!;
        var track = Named<Border>(window, "MediaProgressTrack")!;
        Assert.Equal(83d / 215, fill.Bounds.Width / track.Bounds.Width, 2);
    }

    [AvaloniaFact]
    public void ATrackChange_DisposesTheOldArt_AndKeepsTheNewOne()
    {
        var (window, _, service) = WindowWithMedia(FakeMediaService.Sample() with { AlbumArt = Chisel.App.Dev.SampleArt.Png(64) });
        var image = Named<Image>(window, "MediaArt")!;
        var old = (Bitmap)image.Source!;

        service.Current = FakeMediaService.Sample() with { Title = "Next", AlbumArt = Chisel.App.Dev.SampleArt.Png(48) };
        Dispatcher.UIThread.RunJobs();

        Assert.NotSame(old, image.Source);
        Assert.Throws<ObjectDisposedException>(() => _ = old.PixelSize);
        Assert.Equal(48, ((Bitmap)image.Source!).PixelSize.Width);

        service.Current = FakeMediaService.Sample() with { Title = "Next" };   // no art
        Dispatcher.UIThread.RunJobs();
        Assert.Null(image.Source);
    }

    [AvaloniaFact]
    public void AtTheSmallestSize_NothingIsClipped()
    {
        var (window, _, _) = WindowWithMedia(FakeMediaService.Sample() with { AlbumArt = Chisel.App.Dev.SampleArt.Png() }, weight: 0.01);

        var view = window.GetVisualDescendants().OfType<MediaWidgetView>().Single();
        Assert.True(view.Bounds.Height <= WidgetRegistry.MinimumHeight + 1, $"widget is {view.Bounds.Height} high");
        var inside = new Rect(view.Bounds.Size);
        foreach (var name in new[] { "MediaArt", "MediaTitle", "MediaArtist", "MediaElapsed", "MediaProgressTrack", "MediaPrevious", "MediaPlayPause", "MediaNext" })
        {
            var control = Named<Control>(window, name)!;
            var box = new Rect(control.TranslatePoint(default, view)!.Value, control.Bounds.Size);
            Assert.True(inside.Contains(box.TopLeft) && inside.Contains(box.BottomRight), $"{name} {box} is outside {inside}");
        }
    }

    [AvaloniaFact]
    public void AtTheSmallestSize_TheVolumeRowFits_WithoutCoveringTheButtons()
    {
        var (window, _, _) = WindowWithMedia(FakeMediaService.Sample() with { AlbumArt = Chisel.App.Dev.SampleArt.Png() }, weight: 0.01);

        var view = window.GetVisualDescendants().OfType<MediaWidgetView>().Single();
        var inside = new Rect(view.Bounds.Size);
        Rect Box(string name)
        {
            var control = Named<Control>(window, name)!;
            return new Rect(control.TranslatePoint(default, view)!.Value, control.Bounds.Size);
        }
        foreach (var name in new[] { "MediaMute", "MediaVolumeSlider" })
        {
            var box = Box(name);
            Assert.True(box.Width > 10 && inside.Contains(box.TopLeft) && inside.Contains(box.BottomRight), $"{name} {box} is outside {inside}");
            foreach (var other in new[] { "MediaPrevious", "MediaPlayPause", "MediaNext", "MediaProgressTrack" })
            {
                Assert.False(box.Intersects(Box(other)), $"{name} overlaps {other}");
            }
        }
    }

    [AvaloniaFact]
    public void TheShowVolumeSetting_HidesTheRow()
    {
        var (window, main, _) = WindowWithMedia(FakeMediaService.Sample(), weight: 1.4);
        Assert.True(Named<Button>(window, "MediaMute")!.IsEffectivelyVisible);

        main.MediaSettings.ShowVolume = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named<Button>(window, "MediaMute")!.IsEffectivelyVisible);
        Assert.False(Named<Control>(window, "MediaVolumeSlider")!.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void TheMuteButton_TogglesTheSystemMute_AndNoDeviceDisablesTheRow()
    {
        var volume = new FakeVolumeService();
        var window = UiSnapshot.ShowMainWindow(media: new FakeMediaService { Current = FakeMediaService.Sample() }, volume: volume);
        var main = (MainViewModel)window.DataContext!;
        main.Layout.Apply(main.Layout.Layout.AddWidget(WidgetRegistry.Media, 0, 0, 1.4));
        Dispatcher.UIThread.RunJobs();
        var mute = Named<Button>(window, "MediaMute")!;
        Assert.True(mute.IsEffectivelyEnabled);

        mute.Command!.Execute(null);
        Assert.True(volume.IsMuted);

        volume.IsAvailable = false;
        volume.RaiseChanged();
        Dispatcher.UIThread.RunJobs();
        Assert.False(mute.IsEffectivelyEnabled);
        Assert.False(Named<Control>(window, "MediaVolumeSlider")!.IsEffectivelyEnabled);
    }

    // Looked at by eye: artifacts/ui/media-widget.png (playing, with art, in a tall card above a small one).
    [AvaloniaFact]
    public void Render_WritesAPng()
    {
        var (window, _, _) = WindowWithMedia(FakeMediaService.Sample() with { AlbumArt = Chisel.App.Dev.SampleArt.Png() }, weight: 1.4);

        Assert.True(File.Exists(UiSnapshot.Render(window, "media-widget")));
    }
}
