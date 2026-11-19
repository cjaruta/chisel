using System;
using System.ComponentModel;
using System.IO;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Shapes;
using Chisel.App.ViewModels;
using Chisel.Core.Media;

namespace Chisel.App.Views.Widgets;

// The media widget: cover art, title, artist (album and source app when there is room), a thin progress line with
// elapsed and total time, and previous / play-pause / next. Built once and updated in place. The cover bitmap is
// disposed whenever the track changes and when the widget leaves the window. With no session it says so quietly.
public sealed class MediaWidgetView : WidgetView
{
    private const string PreviousIcon = "M6,5 H8 V19 H6 Z M19,5 V19 L9,12 Z";
    private const string NextIcon = "M16,5 H18 V19 H16 Z M5,5 V19 L15,12 Z";
    private const string PlayIcon = "M7,4 L19,12 L7,20 Z";
    private const string PauseIcon = "M7,5 H10 V19 H7 Z M14,5 H17 V19 H14 Z";
    private const string NoteIcon = "M9,18 V6 L19,4 V16 M9,18 A2.5,2.5 0 1 1 4,18 A2.5,2.5 0 1 1 9,18 M19,16 A2.5,2.5 0 1 1 14,16 A2.5,2.5 0 1 1 19,16";

    private MediaViewModel? _media;
    private MediaSettingsViewModel? _settings;
    private readonly TextBlock _nothing;
    private readonly Grid _player;
    private readonly Border _artBox;
    private readonly Image _art;
    private readonly TextBlock _title, _artist, _album, _source, _elapsed, _duration;
    private readonly Grid _progressFill;
    private readonly Border _track;
    private readonly ColumnDefinition _filled, _rest;
    private readonly Button _previous, _playPause, _next;
    private readonly Path _playPauseIcon;
    private readonly SpectrumBars _bars;
    private byte[]? _shownBytes;
    private Bitmap? _bitmap;

    public MediaWidgetView()
    {
        Padding = new Thickness(16, 12);
        ClipToBounds = true;

        _nothing = Text("Nothing playing", 13, name: "MediaNothing");
        _nothing.FontStyle = FontStyle.Italic;
        _nothing.HorizontalAlignment = HorizontalAlignment.Center;
        _nothing.VerticalAlignment = VerticalAlignment.Center;

        _art = new Image { Name = "MediaArt", Stretch = Stretch.UniformToFill };
        _artBox = new Border
        {
            CornerRadius = new CornerRadius(6), ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0), Child = _art,
        };
        _artBox.Bind(Border.BackgroundProperty, this.GetResourceObservable("GridLineBrush"));

        _title = Text("", 13, name: "MediaTitle", weight: FontWeight.SemiBold);
        _artist = Text("", 12, dim: true, name: "MediaArtist");
        _album = Text("", 11, dim: true, name: "MediaAlbum");
        _source = Text("", 11, dim: true, name: "MediaSource");
        _elapsed = Text("", 11, dim: true, name: "MediaElapsed");
        _duration = Text("", 11, dim: true, name: "MediaDuration");

        _filled = new ColumnDefinition(0, GridUnitType.Star);
        _rest = new ColumnDefinition(1, GridUnitType.Star);
        _progressFill = new Grid { ColumnDefinitions = { _filled, _rest }, Height = 3 };
        var fill = new Border { Name = "MediaProgressFill", CornerRadius = new CornerRadius(1.5) };
        fill.Bind(Border.BackgroundProperty, this.GetResourceObservable("AccentBrush"));
        _progressFill.Children.Add(fill);
        var track = _track = new Border { Name = "MediaProgressTrack", Height = 3, CornerRadius = new CornerRadius(1.5), Child = _progressFill };
        track.Bind(Border.BackgroundProperty, this.GetResourceObservable("GridLineBrush"));
        track.VerticalAlignment = VerticalAlignment.Center;

        var progress = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
        Grid.SetColumn(track, 1);
        Grid.SetColumn(_duration, 2);
        progress.Children.Add(_elapsed);
        progress.Children.Add(track);
        progress.Children.Add(_duration);

        _playPauseIcon = new Path { Data = Geometry.Parse(PlayIcon), Stretch = Stretch.Uniform };
        _previous = IconButton("MediaPrevious", PreviousIcon, "Previous");
        _next = IconButton("MediaNext", NextIcon, "Next");
        _playPause = new Button { Name = "MediaPlayPause", Content = _playPauseIcon };
        _playPause.Classes.Add("mediaButton");
        _playPauseIcon.Bind(Shape.FillProperty, this.GetResourceObservable("CurrentBlockOutlineBrush"));
        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(-4, 0, 0, 0) };
        buttonRow.Children.Add(_previous);
        buttonRow.Children.Add(_playPause);
        buttonRow.Children.Add(_next);
        _bars = new SpectrumBars { Name = "MediaBars", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_bars, 1);
        var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        buttons.Children.Add(buttonRow);
        buttons.Children.Add(_bars);

        var details = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(_title);
        details.Children.Add(_artist);
        details.Children.Add(_album);
        details.Children.Add(_source);
        details.Children.Add(progress);
        details.Children.Add(buttons);

        _player = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(details, 1);
        _player.Children.Add(_artBox);
        _player.Children.Add(details);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is MainViewModel main)
        {
            _media = main.Media;
            _media.PropertyChanged += OnMediaChanged;
            _previous.Command = _media.PreviousCommand;
            _next.Command = _media.NextCommand;
            _playPause.Command = _media.PlayPauseCommand;
            _bars.Source = main.Spectrum;
            _bars.Enabled = main.MediaSettings.ShowVisualizer;
            _settings = main.MediaSettings;
            _settings.PropertyChanged += OnSettingsChanged;
        }
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_media is not null)
        {
            _media.PropertyChanged -= OnMediaChanged;
            _previous.Command = _next.Command = _playPause.Command = null;
            _media = null;
        }
        if (_settings is not null)
        {
            _settings.PropertyChanged -= OnSettingsChanged;
            _settings = null;
        }
        _bars.Playing = false;
        _bars.Source = null;
        ShowArt(null);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        FitToRoom();
    }

    private void OnMediaChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MediaViewModel.ProgressFraction) or nameof(MediaViewModel.ElapsedText))
        {
            ShowProgress();
        }
        else
        {
            Refresh();
        }
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_settings is not null && e.PropertyName == nameof(MediaSettingsViewModel.ShowVisualizer))
        {
            _bars.Enabled = _settings.ShowVisualizer;
        }
    }

    private TextBlock Text(string text, double size, bool dim = false, string? name = null, FontWeight weight = FontWeight.Normal)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight, Name = name, TextTrimming = TextTrimming.CharacterEllipsis };
        block.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(dim ? "DimTextBrush" : "CurrentBlockOutlineBrush"));
        return block;
    }

    private Button IconButton(string name, string icon, string tip)
    {
        var path = new Path { Data = Geometry.Parse(icon), Stretch = Stretch.Uniform };
        path.Bind(Shape.FillProperty, this.GetResourceObservable("CurrentBlockOutlineBrush"));
        var button = new Button { Name = name, Content = path };
        button.Classes.Add("mediaButton");
        ToolTip.SetTip(button, tip);
        return button;
    }

    // Brings every part in line with the view model.
    private void Refresh()
    {
        if (_media is not { HasSession: true } media)
        {
            Content = _nothing;
            _bars.Playing = false;
            ShowArt(null);
            return;
        }
        _title.Text = media.Title;
        _artist.Text = media.Artist;
        _album.Text = media.Album;
        _source.Text = media.Source;
        _playPauseIcon.Data = Geometry.Parse(media.IsPlaying ? PauseIcon : PlayIcon);
        _playPause.Tag = media.IsPlaying ? "Pause" : "Play";
        ToolTip.SetTip(_playPause, media.IsPlaying ? "Pause" : "Play");
        ShowArt(media.ArtBytes);
        ShowProgress();
        FitToRoom();
        Content = _player;
        _bars.Playing = media.IsPlaying;
    }

    private void ShowProgress()
    {
        if (_media is null)
        {
            return;
        }
        var fraction = _media.ProgressFraction;
        _filled.Width = new GridLength(fraction, GridUnitType.Star);
        _rest.Width = new GridLength(1 - fraction, GridUnitType.Star);
        _elapsed.Text = _media.ElapsedText;
        _duration.Text = _media.DurationText;
        _track.IsVisible = _media.HasTimeline;
        _elapsed.IsVisible = _duration.IsVisible = _media.HasTimeline;
    }

    // Shows or hides the cover, album and source to suit the room (see MediaLayout).
    private void FitToRoom()
    {
        if (_media is null)
        {
            return;
        }
        var plan = MediaLayout.For(Bounds.Width - Padding.Left - Padding.Right, Bounds.Height - Padding.Top - Padding.Bottom);
        _artBox.IsVisible = plan.ArtSize > 0;
        _artBox.Width = _artBox.Height = Math.Max(plan.ArtSize, 0);
        _album.IsVisible = plan.ShowAlbum && _media.Album != "";
        _source.IsVisible = plan.ShowSource && _media.Source != "";
    }

    // Decodes the cover (or clears it), disposing the one it replaces. Bad image bytes just mean no cover.
    private void ShowArt(byte[]? bytes)
    {
        if (ReferenceEquals(bytes, _shownBytes) && (bytes is null || _bitmap is not null))
        {
            return;
        }
        _shownBytes = bytes;
        _art.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
        if (bytes is null)
        {
            return;
        }
        try
        {
            _bitmap = new Bitmap(new MemoryStream(bytes));
            _art.Source = _bitmap;
        }
        catch (Exception)
        {
            _bitmap = null;
        }
    }
}
