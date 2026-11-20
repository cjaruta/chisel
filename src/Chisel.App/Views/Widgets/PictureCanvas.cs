using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;
using Chisel.Core.Pictures;

namespace Chisel.App.Views.Widgets;

// A decoded picture and a tiny copy of it, stretched up behind a fitted picture as the "blur fill".
public sealed class PictureLayer(Bitmap bitmap, Bitmap? blur) : IDisposable
{
    public Bitmap Bitmap { get; } = bitmap;
    public Bitmap? Blur { get; } = blur;

    public void Dispose()
    {
        Bitmap.Dispose();
        Blur?.Dispose();
    }
}

// Where a picture is panned and zoomed (Fill mode).
public readonly record struct PictureCrop(double FocalX = 0.5, double FocalY = 0.5, double Zoom = 1);

// Draws one or two pictures with the frame's options: Fill crops to the card around the picture's focal point, Fit shows
// all of it (with a blurred copy behind when asked). A fade draws the old picture and the new one over it. The canvas only
// draws; the view owns and disposes the bitmaps. Colors come from theme tokens.
public sealed class PictureCanvas : Control
{
    private PictureLayer? _current;
    private PictureLayer? _previous;
    private PictureCrop _currentCrop;
    private PictureCrop _previousCrop;
    private double _currentOpacity = 1;
    private FitMode _fit;
    private bool _blurFill;
    private bool _forceFill;
    private bool _loading;
    private CornerRadius _corners = new(12);

    public PictureCanvas()
    {
        IsHitTestVisible = true;
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
    }

    public PictureLayer? Current => _current;

    public CornerRadius Corners
    {
        get => _corners;
        set
        {
            _corners = value;
            InvalidateVisual();
        }
    }

    public void SetOptions(FitMode fit, bool blurFill, bool forceFill)
    {
        _fit = fit;
        _blurFill = blurFill;
        _forceFill = forceFill;
        InvalidateVisual();
    }

    // Shows a picture at once (no fade), or none.
    public void SetCurrent(PictureLayer? layer, PictureCrop crop)
    {
        _previous = null;
        _current = layer;
        _currentCrop = crop;
        _currentOpacity = 1;
        InvalidateVisual();
    }

    // The crop of the picture shown changed (the user is adjusting it).
    public void SetCrop(PictureCrop crop)
    {
        _currentCrop = crop;
        InvalidateVisual();
    }

    // Starts a fade: the picture shown now stays under the new one while it rises from 0 to 1 (see SetFade).
    public void BeginFade(PictureLayer? next, PictureCrop crop)
    {
        _previous = _current;
        _previousCrop = _currentCrop;
        _current = next;
        _currentCrop = crop;
        _currentOpacity = 0;
        InvalidateVisual();
    }

    public void SetFade(double progress)
    {
        _currentOpacity = Math.Clamp(progress, 0, 1);
        InvalidateVisual();
    }

    public void EndFade()
    {
        _previous = null;
        _currentOpacity = 1;
        InvalidateVisual();
    }

    public bool IsFading => _previous is not null;

    public void SetLoading(bool loading)
    {
        _loading = loading;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        using var clip = context.PushClip(new RoundedRect(bounds, _corners.TopLeft, _corners.TopRight, _corners.BottomRight, _corners.BottomLeft));
        context.FillRectangle(Theming.ThemeService.Brush(this, "CardBackgroundBrush"), bounds);

        if (_previous is not null)
        {
            Draw(context, bounds, _previous, _previousCrop);
        }
        if (_current is not null)
        {
            using (context.PushOpacity(_currentOpacity))
            {
                Draw(context, bounds, _current, _currentCrop);
            }
        }
        if (_loading)
        {
            DrawSpinner(context, bounds);
        }
    }

    private void Draw(DrawingContext context, Rect bounds, PictureLayer layer, PictureCrop crop)
    {
        var size = layer.Bitmap.PixelSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        if (_fit == FitMode.Fill || _forceFill)
        {
            var src = CropMath.SourceRect(size.Width, size.Height, bounds.Width, bounds.Height, crop.FocalX, crop.FocalY, crop.Zoom);
            context.DrawImage(layer.Bitmap, new Rect(src.X, src.Y, src.Width, src.Height), bounds);
            return;
        }

        if (_blurFill && layer.Blur is { } blur)
        {
            var blurSize = blur.PixelSize;
            var src = CropMath.SourceRect(blurSize.Width, blurSize.Height, bounds.Width, bounds.Height, 0.5, 0.5, 1);
            context.DrawImage(blur, new Rect(src.X, src.Y, src.Width, src.Height), bounds);
            // Softened toward the card color, so the fitted picture stands out and the fill never competes with it.
            using (context.PushOpacity(0.45))
            {
                context.FillRectangle(Theming.ThemeService.Brush(this, "CardBackgroundBrush"), bounds);
            }
        }
        var scale = Math.Min(bounds.Width / size.Width, bounds.Height / size.Height);
        var width = size.Width * scale;
        var height = size.Height * scale;
        context.DrawImage(layer.Bitmap, new Rect(0, 0, size.Width, size.Height),
            new Rect((bounds.Width - width) / 2, (bounds.Height - height) / 2, width, height));
    }

    // A quarter-turn arc that spins while a dropped picture is on its way.
    private void DrawSpinner(DrawingContext context, Rect bounds)
    {
        var radius = Math.Min(14, Math.Min(bounds.Width, bounds.Height) / 4);
        var center = bounds.Center;
        var angle = Environment.TickCount64 % 900 / 900.0 * 2 * Math.PI;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            var start = new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
            var end = new Point(center.X + radius * Math.Cos(angle + 1.5 * Math.PI), center.Y + radius * Math.Sin(angle + 1.5 * Math.PI));
            g.BeginFigure(start, false);
            g.ArcTo(end, new Size(radius, radius), 0, true, SweepDirection.Clockwise);
            g.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(Theming.ThemeService.Brush(this, "AccentBrush"), 2.5, lineCap: PenLineCap.Round), geometry);
    }
}
