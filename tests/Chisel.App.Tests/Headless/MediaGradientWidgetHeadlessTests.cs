using System;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.Services;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.Core.Media;

namespace Chisel.App.Tests.Headless;

public class MediaGradientWidgetHeadlessTests
{
    private static (Chisel.App.Views.MainWindow Window, MainViewModel Main, FakeMediaService Service) WindowWithMedia(MediaSessionInfo? info, double weight = 1.6)
    {
        var service = new FakeMediaService { Current = info };
        var window = UiSnapshot.ShowMainWindow(media: service);
        var main = (MainViewModel)window.DataContext!;
        var layout = main.Layout.Layout;
        main.Layout.Apply(layout.AddWidget(WidgetRegistry.Media, 0, layout.Columns[0].Widgets.Count, weight));
        Settle(main);
        return (window, main, service);
    }

    // Lets the background palette work finish and its result reach the view.
    private static void Settle(MainViewModel main)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        do
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        while (!main.MediaBackground.Settled.IsCompleted && DateTime.UtcNow < until);
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Chisel.App.Views.MainWindow w, string name) where T : Control =>
        w.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static MediaSessionInfo WithArt(MediaPlaybackState state = MediaPlaybackState.Playing) =>
        FakeMediaService.Sample(state) with { AlbumArt = Chisel.App.Dev.SampleArt.Png() };

    [AvaloniaFact]
    public void TheGradientIsOnByDefault_AndShowsTheCoversPalette()
    {
        var (window, main, _) = WindowWithMedia(WithArt());
        var gradient = Named<AlbumGradientBackground>(window, "MediaGradient");

        Assert.True(gradient.IsVisible);
        Assert.Equal(main.MediaBackground.Colors, gradient.Colors);
        Assert.NotEmpty(gradient.Colors);
    }

    [AvaloniaFact]
    public void WithTheSettingOff_ItIsTheNormalCard_AndTheTextGoesBackToTheThemeColors()
    {
        var (window, main, _) = WindowWithMedia(WithArt());
        var title = Named<TextBlock>(window, "MediaTitle");
        var onGradient = ((ISolidColorBrush)title.Foreground!).Color;

        main.MediaSettings.AlbumGradient = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named<AlbumGradientBackground>(window, "MediaGradient").IsVisible);
        var card = ((ISolidColorBrush)Chisel.App.Theming.ThemeService.Brush(title, "CurrentBlockOutlineBrush")).Color;
        Assert.Equal(card, ((ISolidColorBrush)title.Foreground!).Color);
        Assert.NotEqual(onGradient, card);
    }

    [AvaloniaFact]
    public void TheGradientFillsTheBodyBelowTheHeader_AndTheWholeCardWithoutOne()
    {
        var (window, _, _) = WindowWithMedia(WithArt());
        var gradient = Named<AlbumGradientBackground>(window, "MediaGradient");
        var view = gradient.FindAncestorOfType<MediaWidgetView>()!;
        var frame = view.FindAncestorOfType<WidgetFrame>()!;
        frame.ShowHeader = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.Bounds.Size == gradient.Bounds.Size, $"view {view.Bounds} padding {view.Padding} border {view.BorderThickness} gradient {gradient.Bounds} margin {gradient.Margin}");
        Assert.Equal(new CornerRadius(0, 0, 12, 12), gradient.CornerRadius);
        var top = gradient.TranslatePoint(default, frame)!.Value.Y;
        Assert.True(top >= frame.Header.Bounds.Height - 0.5, "the header stays plain above the gradient");

        frame.ShowHeader = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new CornerRadius(12), gradient.CornerRadius);
        Assert.True(gradient.TranslatePoint(default, frame)!.Value.Y <= 2, "no header: the gradient starts at the top of the card");
        Assert.True(frame.Bounds.Width - gradient.Bounds.Width <= 2 && frame.Bounds.Height - gradient.Bounds.Height <= 2, "and fills the card inside its border");
    }

    [AvaloniaFact]
    public void TheDriftFollowsPlayback_AndTheAnimateSetting()
    {
        var (window, main, service) = WindowWithMedia(WithArt());
        var gradient = Named<AlbumGradientBackground>(window, "MediaGradient");
        Assert.True(gradient.Animate);

        service.Current = WithArt(MediaPlaybackState.Paused);
        Dispatcher.UIThread.RunJobs();
        Assert.False(gradient.Animate);
        Assert.False(gradient.IsRunning);

        service.Current = WithArt();
        Dispatcher.UIThread.RunJobs();
        Assert.True(gradient.Animate);

        main.MediaSettings.AnimateGradient = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(gradient.Animate);
    }

    [AvaloniaFact]
    public void WithNoSession_TheGradientIsTheThemeFallback()
    {
        var (window, main, _) = WindowWithMedia(null);
        var gradient = Named<AlbumGradientBackground>(window, "MediaGradient");

        Assert.True(gradient.IsVisible);
        Assert.False(gradient.Animate);
        Assert.Equal(AlbumGradient.Fallback(main.MediaBackground.Mode, Chisel.Core.Theme.ThemeModes.Accent(main.MediaBackground.Mode, Chisel.Core.Theme.ThemeModes.ResolveAccent(null)).Fill), gradient.Colors);
    }
}
