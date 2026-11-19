using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Gif;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chisel.App.ViewModels;
using Chisel.Core.Pictures;

namespace Chisel.App.Views.Widgets;

// A picture or a slideshow that fills its card. Everything about what to show and when lives in PictureFrameViewModel; this
// view decodes the picture shown and the one that comes next (never more), draws it with PictureCanvas, fades between
// pictures, plays an animated GIF only while the frame is on screen, and turns pointer, wheel and keys into the frame's
// click, arrow and adjust actions. Clicking the right half goes forward, the left half back; there are no buttons.
public sealed class PictureFrameView : WidgetView, IDisposable
{
    public static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(400);
    private const int LongSide = 2560;

    private readonly PictureCanvas _canvas = new() { Name = "PictureCanvas" };
    private readonly Border _clip = new() { Name = "PictureClip", ClipToBounds = true };
    private readonly Panel _stack = new();
    private readonly TextBlock _hint;
    private readonly Border _adjustBar;
    private readonly Dictionary<Guid, PictureLayer> _cache = new();
    private GifImage? _gif;
    private FileStream? _gifStream;
    private MainViewModel? _main;
    private PictureFrameViewModel? _vm;
    private DispatcherTimer? _timer;
    private long _fadeStart;
    private int _version;
    private Point? _press;
    private Point _lastPan;
    private bool _panning;
    private bool _disposed;

    public PictureFrameView()
    {
        Focusable = true;
        Padding = new Thickness(0);
        AutomationProperties.SetName(this, "Picture frame");
        AutomationProperties.SetHelpText(this, "Left and Right arrow keys show the previous or next picture");

        _hint = new TextBlock
        {
            Name = "PictureHint",
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24, 0),
            IsHitTestVisible = false,
            IsVisible = false,
        };
        _hint.Bind(TextBlock.ForegroundProperty, _hint.GetResourceObservable("DimTextBrush"));

        var reset = new Button { Name = "PictureAdjustReset", Content = "Reset" };
        var done = new Button { Name = "PictureAdjustDone", Content = "Done" };
        reset.Click += (_, _) => _vm?.ResetCrop();
        done.Click += (_, _) => _main?.Layout.EndAdjust();
        var text = new TextBlock { Text = "Drag to move, scroll to zoom", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        _adjustBar = new Border
        {
            Name = "PictureAdjustBar",
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(12),
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(8),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { text, reset, done } },
        };
        _adjustBar.Classes.Add("card");

        _stack.Children.Add(_canvas);
        _stack.Children.Add(_hint);
        _stack.Children.Add(_adjustBar);
        _clip.Child = _stack;
        Content = _clip;
        ApplyCorners();
    }

    public PictureCanvas Canvas => _canvas;

    public GifImage? Gif => _gif;

    public bool IsFading => _canvas.IsFading;

    // Whether an animated picture is on screen and playing (not paused).
    public bool IsPlayingGif => _gif is not null;

    // How many pictures are decoded right now: at most the one shown and the one that comes next.
    public int DecodedCount => _cache.Count;

    public PictureFrameViewModel? Frame => _vm;

    protected override void OnHeaderShownChanged() => ApplyCorners();

    private void ApplyCorners()
    {
        var corners = HeaderShown ? new CornerRadius(0, 0, 12, 12) : new CornerRadius(12);
        _clip.CornerRadius = corners;
        _canvas.Corners = corners;
    }

    // ----- life with the view model

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Bind();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unbind();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Bind();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_vm is not null && (change.Property == IsVisibleProperty || change.Property.Name == nameof(IsEffectivelyVisible)))
        {
            _vm.IsVisible = IsEffectivelyVisible && this.IsAttachedToVisualTree();
            UpdateTimer();
        }
    }

    private void Bind()
    {
        if (_disposed || _vm is not null || !this.IsAttachedToVisualTree() || DataContext is not MainViewModel main || InstanceId is not { } id)
        {
            return;
        }
        _main = main;
        _vm = main.PictureFrames.For(id);
        _vm.CurrentChanged += OnCurrentChanged;
        _vm.Changed += OnChanged;
        _vm.RunningChanged += OnRunningChanged;
        _vm.AdjustStateChanged += OnAdjustChanged;
        _vm.IsVisible = IsEffectivelyVisible;
        ApplyOptions();
        OnAdjustChanged();
        LoadCurrent(transition: false);
        UpdateHint();
    }

    private void Unbind()
    {
        if (_vm is null)
        {
            return;
        }
        _vm.IsVisible = false;
        _vm.CurrentChanged -= OnCurrentChanged;
        _vm.Changed -= OnChanged;
        _vm.RunningChanged -= OnRunningChanged;
        _vm.AdjustStateChanged -= OnAdjustChanged;
        _vm = null;
        HideGif();
        _version++;
        StopTimer();
        // Off the dashboard or on its way to another card: nothing stays decoded (it is decoded again when shown).
        ReleaseBitmaps();
    }

    private void ReleaseBitmaps()
    {
        _canvas.SetCurrent(null, default);
        foreach (var layer in _cache.Values)
        {
            layer.Dispose();
        }
        _cache.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        var main = _main;
        var id = InstanceId;
        Unbind();
        _disposed = true;
        ReleaseBitmaps();
        if (main is not null && id is not null)
        {
            main.PictureFrames.Forget(id);
        }
    }

    private void OnCurrentChanged() => LoadCurrent(transition: true);

    private void OnRunningChanged()
    {
        // An animated picture plays only while the frame runs: switch between the moving and the still picture.
        if (_vm?.Current is { } picture && _main?.PictureFrames.AssetOf(picture.AssetId) is { IsGif: true })
        {
            LoadCurrent(transition: false);
        }
        UpdateTimer();
    }

    private void OnChanged()
    {
        if (_vm is null)
        {
            return;
        }
        ApplyOptions();
        if (_vm.Current is { } picture && !_canvas.IsFading)
        {
            _canvas.SetCrop(new PictureCrop(picture.FocalX, picture.FocalY, picture.Zoom));
        }
        UpdateHint();
        UpdateTimer();
    }

    private void OnAdjustChanged()
    {
        _adjustBar.IsVisible = _vm?.IsAdjusting == true;
        ApplyOptions();
        if (_vm?.IsAdjusting == true)
        {
            // The still picture is adjusted, so an animated one pauses on its first frame.
            LoadCurrent(transition: false);
        }
    }

    private void ApplyOptions()
    {
        if (_vm is null)
        {
            return;
        }
        var options = _vm.Content.Options;
        _canvas.SetOptions(options.Fit, options.BlurFill, _vm.IsAdjusting);
        if (_gif is not null)
        {
            _gif.Stretch = options.Fit == FitMode.Fill ? Stretch.UniformToFill : Stretch.Uniform;
        }
    }

    private void UpdateHint()
    {
        var empty = _vm is not null && _vm.Count == 0 && _vm.Pending == 0;
        _hint.IsVisible = empty;
        _hint.Text = "Empty picture frame.\nIn Customize layout, drag pictures here.";
        _canvas.SetLoading(_vm is { Pending: > 0 });
    }

    // ----- pictures

    private async void LoadCurrent(bool transition)
    {
        if (_vm is null || _main is null)
        {
            return;
        }
        var vm = _vm;
        var mine = ++_version;
        var frames = _main.PictureFrames;
        if (vm.Current is not { } picture || frames.AssetOf(picture.AssetId) is not { } asset || frames.Catalog is not { } catalog)
        {
            HideGif();
            _canvas.SetCurrent(null, default);
            UpdateHint();
            return;
        }

        var path = catalog.PathOf(asset);
        var crop = new PictureCrop(picture.FocalX, picture.FocalY, picture.Zoom);
        if (asset.IsGif && vm.ShouldAnimate && !vm.IsAdjusting && File.Exists(path))
        {
            ShowGif(path);
            _canvas.SetCurrent(null, crop);
            Evict();
            return;
        }
        HideGif();

        var layer = await LayerFor(asset, path);
        if (mine != _version || _vm != vm)
        {
            return;
        }
        var fade = transition && layer is not null && _canvas.Current is not null && vm.IsRunning
            && vm.Content.Options.Transition == SlideshowTransition.Fade;
        if (fade)
        {
            _canvas.BeginFade(layer, crop);
            _fadeStart = Stopwatch.GetTimestamp();
        }
        else
        {
            _canvas.SetCurrent(layer, crop);
            Evict();
        }
        UpdateTimer();
        Preload(vm.Next);
    }

    private async Task<PictureLayer?> LayerFor(PictureAsset asset, string path)
    {
        if (_cache.TryGetValue(asset.Id, out var cached))
        {
            return cached;
        }
        var decoded = await Task.Run(() => Decode(path, asset));
        if (decoded is null)
        {
            return null;
        }
        if (_disposed)
        {
            decoded.Dispose();
            return null;
        }
        if (_cache.TryGetValue(asset.Id, out var raced))
        {
            decoded.Dispose();
            return raced;
        }
        _cache[asset.Id] = decoded;
        return decoded;
    }

    private async void Preload(FramePicture? next)
    {
        if (next is null || _main is null || _main.PictureFrames.AssetOf(next.AssetId) is not { } asset
            || asset.IsGif || _main.PictureFrames.Catalog is not { } catalog)
        {
            return;
        }
        await LayerFor(asset, catalog.PathOf(asset));
    }

    // Decodes at most LongSide pixels on the long side, so a huge picture never costs more than it can show.
    private static PictureLayer? Decode(string path, PictureAsset asset)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var width = asset.Width >= asset.Height
                ? Math.Min(asset.Width, LongSide)
                : Math.Min(asset.Width, (int)Math.Round(LongSide * (double)asset.Width / asset.Height));
            var bitmap = Bitmap.DecodeToWidth(stream, Math.Max(1, width), BitmapInterpolationMode.HighQuality);
            var size = bitmap.PixelSize;
            var blur = bitmap.CreateScaledBitmap(new PixelSize(24, Math.Max(1, (int)Math.Round(24.0 * size.Height / size.Width))), BitmapInterpolationMode.HighQuality);
            return new PictureLayer(bitmap, blur);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    // Keeps the picture shown and the one that comes next, and lets the rest go.
    private void Evict()
    {
        if (_vm is null || _main is null || _canvas.IsFading)
        {
            return;
        }
        var keep = new HashSet<Guid>();
        if (_vm.Current is { } current)
        {
            keep.Add(current.AssetId);
        }
        if (_vm.Next is { } next)
        {
            keep.Add(next.AssetId);
        }
        foreach (var (id, layer) in _cache.Where(c => !keep.Contains(c.Key)).ToList())
        {
            if (!ReferenceEquals(layer, _canvas.Current))
            {
                layer.Dispose();
                _cache.Remove(id);
            }
        }
    }

    private void ShowGif(string path)
    {
        HideGif();
        try
        {
            _gifStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            _gif = new GifImage
            {
                Name = "PictureGif",
                IsHitTestVisible = false,
                Stretch = _vm?.Content.Options.Fit == FitMode.Fit ? Stretch.Uniform : Stretch.UniformToFill,
                Source = GifStreamSource.FromStream(_gifStream),
            };
            _stack.Children.Insert(1, _gif);
        }
        catch (IOException)
        {
            HideGif();
        }
    }

    private void HideGif()
    {
        if (_gif is not null)
        {
            _stack.Children.Remove(_gif);
            _gif.Source = null;
            _gif = null;
        }
        _gifStream?.Dispose();
        _gifStream = null;
    }

    // ----- the timer: only while a fade runs or a dropped picture is loading, and only while the frame is on screen

    private void UpdateTimer()
    {
        var needed = _vm is { } vm && IsEffectivelyVisible && (_canvas.IsFading || vm.Pending > 0);
        if (!needed)
        {
            StopTimer();
            return;
        }
        _timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => OnTimer());
        _timer.Start();
    }

    private void StopTimer() => _timer?.Stop();

    private void OnTimer()
    {
        if (_canvas.IsFading)
        {
            var progress = Stopwatch.GetElapsedTime(_fadeStart) / FadeTime;
            if (progress >= 1)
            {
                CompleteFade();
            }
            else
            {
                _canvas.SetFade(progress);
            }
        }
        if (_vm?.Pending > 0)
        {
            _canvas.InvalidateVisual();
        }
        UpdateTimer();
    }

    // Ends a fade at once (the timer calls it when the time is up).
    public void CompleteFade()
    {
        _canvas.EndFade();
        Evict();
        UpdateTimer();
    }

    // ----- input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_vm is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var point = e.GetPosition(this);
        if (_vm.IsAdjusting)
        {
            _panning = true;
            _lastPan = point;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        Focus();
        _press = point;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_panning && _vm is not null)
        {
            var point = e.GetPosition(this);
            _vm.AdjustPan(Bounds.Width, Bounds.Height, point.X - _lastPan.X, point.Y - _lastPan.Y);
            _lastPan = point;
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panning)
        {
            _panning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }
        if (_press is { } start && _vm is not null)
        {
            var point = e.GetPosition(this);
            if (_vm.HandleClick(point.X, Bounds.Width, point.X - start.X, point.Y - start.Y))
            {
                e.Handled = true;
            }
        }
        _press = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_vm?.IsAdjusting == true)
        {
            _vm.AdjustZoom(e.Delta.Y);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_vm?.HandleKey(e.Key) == true)
        {
            e.Handled = true;
        }
    }
}
