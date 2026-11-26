using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace Chisel.App.Startup;

// A connected screen: its working area (pixels, without the taskbar) and its scaling (1.5 at 150%).
public sealed record ScreenArea(PixelRect WorkingArea, double Scaling, bool IsPrimary);

// Where and how big the window opens.
public sealed record WindowRestore(PixelPoint Position, double Width, double Height, bool Maximized);

public static class WindowPlacementPolicy
{
    public const double DefaultWidth = 1200;
    public const double DefaultHeight = 760;
    public const double MinWidth = 640;
    public const double MinHeight = 420;

    // The part of the title bar that must be on a screen for the saved place to count, in pixels.
    private const int StripWidth = 240;
    private const int StripHeight = 40;

    // The saved place when its title bar is still on a connected screen (monitor still there, same layout), fitted to that
    // screen's current size and scaling; otherwise a default window centered on the primary screen. Never smaller than the
    // minimum, never larger than the screen, never partly off it. Null when no screen is known (nothing to check against).
    public static WindowRestore? Restore(WindowPlacement? saved, IReadOnlyList<ScreenArea> screens)
    {
        if (screens.Count == 0)
        {
            return null;
        }

        if (saved is { X: { } x, Y: { } y } && ScreenUnder(saved, (int)x, (int)y, screens) is { } screen)
        {
            return Fit(screen, saved.Width, saved.Height, (int)x, (int)y, saved.Maximized);
        }

        var primary = screens.FirstOrDefault(s => s.IsPrimary) ?? screens[0];
        var fitted = Fit(primary, DefaultWidth, DefaultHeight, primary.WorkingArea.X, primary.WorkingArea.Y, false);
        var area = primary.WorkingArea;
        var (widthPx, heightPx) = Pixels(fitted.Width, fitted.Height, primary.Scaling);
        return fitted with { Position = new PixelPoint(area.X + (area.Width - widthPx) / 2, area.Y + (area.Height - heightPx) / 2) };
    }

    // The screen showing most of the title bar strip, if at least half of it is on one.
    private static ScreenArea? ScreenUnder(WindowPlacement saved, int x, int y, IReadOnlyList<ScreenArea> screens)
    {
        var strip = new PixelRect(x, y, StripWidth, StripHeight);
        var best = screens
            .Select(s => (Screen: s, Area: Area(s.WorkingArea.Intersect(strip))))
            .OrderByDescending(s => s.Area)
            .First();
        return best.Area * 2 >= StripWidth * StripHeight ? best.Screen : null;
    }

    private static long Area(PixelRect r) => (long)r.Width * r.Height;

    private static WindowRestore Fit(ScreenArea screen, double width, double height, int x, int y, bool maximized)
    {
        var area = screen.WorkingArea;
        var maxWidth = area.Width / screen.Scaling;
        var maxHeight = area.Height / screen.Scaling;
        var w = Math.Min(Math.Max(width, MinWidth), maxWidth);
        var h = Math.Min(Math.Max(height, MinHeight), maxHeight);
        var (widthPx, heightPx) = Pixels(w, h, screen.Scaling);
        var px = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - widthPx));
        var py = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - heightPx));
        return new WindowRestore(new PixelPoint(px, py), w, h, maximized);
    }

    private static (int Width, int Height) Pixels(double width, double height, double scaling) =>
        ((int)Math.Ceiling(width * scaling), (int)Math.Ceiling(height * scaling));
}

// Follows the window and keeps what to save: the last normal bounds, and whether it is maximized. Minimizing changes nothing
// (a minimized window reports a far off-screen position), and maximizing keeps the bounds it will go back to.
public sealed class WindowPlacementTracker
{
    private WindowPlacement? _normal;
    private bool _maximized;

    public WindowPlacement? Current => _normal is null ? null : _normal with { Maximized = _maximized };

    public void Observe(WindowState state, double width, double height, PixelPoint position)
    {
        if (state == WindowState.Normal)
        {
            _normal = new WindowPlacement(width, height, position.X, position.Y);
            _maximized = false;
        }
        else if (state is WindowState.Maximized or WindowState.FullScreen)
        {
            _maximized = true;
        }
    }
}
