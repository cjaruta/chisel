using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Chisel.App.Views.Widgets;
using Chisel.Core.Theme;

namespace Chisel.App.Tests.Headless;

public class AlbumGradientBackgroundTests
{
    private static readonly Size Box = new(300, 120);
    private static readonly Rgb[] RedToBlue = [new(180, 30, 30), new(60, 60, 60), new(30, 30, 180)];
    private static readonly Rgb[] GreenToGreen = [new(30, 160, 30), new(40, 150, 40), new(30, 160, 30)];

    private static RenderTargetBitmap Render(AlbumGradientBackground control)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize((int)Box.Width, (int)Box.Height));
        using var context = bitmap.CreateDrawingContext();
        control.DrawTo(context, Box);
        return bitmap;
    }

    private static (byte R, byte G, byte B, byte A) Pixel(RenderTargetBitmap bitmap, int x, int y)
    {
        var data = new byte[bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4];
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), handle.AddrOfPinnedObject(), data.Length, bitmap.PixelSize.Width * 4);
        }
        finally
        {
            handle.Free();
        }
        var i = (y * bitmap.PixelSize.Width + x) * 4;
        // Pixels come back premultiplied and in the bitmap's own channel order; the checks below only compare relative sizes.
        return bitmap.Format == Avalonia.Platform.PixelFormats.Bgra8888 ? (data[i + 2], data[i + 1], data[i], data[i + 3]) : (data[i], data[i + 1], data[i + 2], data[i + 3]);
    }

    [AvaloniaFact]
    public void Draw_PaintsTheGradientAcrossTheCard()
    {
        var control = new AlbumGradientBackground();
        control.SetColors(RedToBlue, fade: false);
        var bitmap = Render(control);

        var left = Pixel(bitmap, 20, 60);
        var right = Pixel(bitmap, 280, 60);
        Assert.Equal(255, left.A);
        Assert.True(left.R > right.R, "red end is on the left");
        Assert.True(right.B > left.B, "blue end is on the right");
    }

    [AvaloniaFact]
    public void Corners_FollowTheCornerRadius()
    {
        var control = new AlbumGradientBackground { CornerRadius = new CornerRadius(12) };
        control.SetColors(RedToBlue, fade: false);
        Assert.Equal(0, Pixel(Render(control), 0, 0).A);

        control.CornerRadius = new CornerRadius(0, 0, 12, 12);
        var bitmap = Render(control);
        Assert.Equal(255, Pixel(bitmap, 0, 0).A);
        Assert.Equal(0, Pixel(bitmap, 0, 119).A);
    }

    [AvaloniaFact]
    public void ANewPalette_CrossFadesOverHalfASecond()
    {
        var control = new AlbumGradientBackground();
        control.SetColors(RedToBlue, fade: false);
        control.SetColors(GreenToGreen, fade: true);
        Assert.True(control.IsFading);

        var start = Pixel(Render(control), 20, 60);
        control.Advance(0.25);
        var middle = Pixel(Render(control), 20, 60);
        control.Advance(0.4);
        var end = Pixel(Render(control), 20, 60);

        Assert.True(start.R > middle.R && middle.R > end.R, "red fades out");
        Assert.True(start.G < middle.G && middle.G < end.G, "green fades in");
        Assert.False(control.IsFading);
    }

    [AvaloniaFact]
    public void WithoutFade_ThePaletteChangesAtOnce()
    {
        var control = new AlbumGradientBackground();
        control.SetColors(RedToBlue, fade: false);
        control.SetColors(GreenToGreen, fade: false);
        Assert.False(control.IsFading);
    }

    private static (Window Window, AlbumGradientBackground Control) Shown()
    {
        var control = new AlbumGradientBackground();
        control.SetColors(RedToBlue, fade: false);
        var window = new Window { Width = 300, Height = 120, Content = control };
        window.Show();
        return (window, control);
    }

    [AvaloniaFact]
    public void Motion_RunsOnlyWhileAnimatingAndVisible_AndStopsWhenPaused()
    {
        var (window, control) = Shown();
        Assert.False(control.IsRunning);

        control.Animate = true;
        Assert.True(control.IsRunning);

        control.Animate = false;
        Assert.False(control.IsRunning);

        control.Animate = true;
        control.IsVisible = false;
        Assert.False(control.IsRunning);

        control.IsVisible = true;
        window.WindowState = WindowState.Minimized;
        Assert.False(control.IsRunning);

        window.WindowState = WindowState.Normal;
        Assert.True(control.IsRunning);

        window.Close();
        Assert.False(control.IsRunning);
    }

    [AvaloniaFact]
    public void TheDrift_MovesOnlyWhileAnimating_AndLoops()
    {
        var (window, control) = Shown();
        var before = control.Phase;

        control.Advance(5);
        Assert.Equal(before, control.Phase); // not animating: nothing moves

        control.Animate = true;
        control.Advance(5);
        Assert.NotEqual(before, control.Phase);

        control.Advance(AlbumGradientBackground.LoopSeconds - 5);
        Assert.Equal(before, control.Phase);
        window.Close();
    }

    [AvaloniaFact]
    public void TheDrift_ChangesTheDrawnGradientOnlySlightly()
    {
        var control = new AlbumGradientBackground { Animate = true };
        control.SetColors(RedToBlue, fade: false);
        var a = Pixel(Render(control), 150, 60);
        control.Advance(AlbumGradientBackground.LoopSeconds / 2);
        var b = Pixel(Render(control), 150, 60);
        Assert.NotEqual(a, b);
        Assert.True(Math.Abs(a.R - b.R) < 40 && Math.Abs(a.B - b.B) < 40, "a few percent of drift, not a different picture");
    }

    // Once a palette's bitmaps exist, drawing frames (drifting, or mid cross-fade) allocates next to nothing: only the rounded
    // clip (under 100 bytes a frame) is left, and that comes from Avalonia.
    [AvaloniaFact]
    public void Draw_AllocatesNothingInSteadyState()
    {
        const int frames = 300;
        var control = new AlbumGradientBackground { Animate = true, CornerRadius = new CornerRadius(12) };
        control.SetColors(RedToBlue, fade: false);
        var bitmap = new RenderTargetBitmap(new PixelSize((int)Box.Width, (int)Box.Height));
        using var context = bitmap.CreateDrawingContext();
        for (var i = 0; i < 12; i++)
        {
            control.Advance(0.1);
            control.DrawTo(context, Box);
        }
        // Best of three rounds, so one-off runtime noise (JIT, tiering) can't fail a steady frame.
        var drifting = long.MaxValue;
        for (var round = 0; round < 3; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < frames; i++)
            {
                control.Advance(0.1);
                control.DrawTo(context, Box);
            }
            drifting = Math.Min(drifting, (GC.GetAllocatedBytesForCurrentThread() - before) / frames);
        }

        control.SetColors(GreenToGreen, fade: true);
        control.DrawTo(context, Box);
        var fading = long.MaxValue;
        for (var round = 0; round < 3; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < frames; i++)
            {
                control.DrawTo(context, Box); // the fade does not advance without Advance, so these are all cross-fade frames
            }
            fading = Math.Min(fading, (GC.GetAllocatedBytesForCurrentThread() - before) / frames);
        }

        Assert.True(drifting <= 128, $"drifting: {drifting} B per frame");
        Assert.True(fading <= 128, $"cross-fade: {fading} B per frame");
    }
}
