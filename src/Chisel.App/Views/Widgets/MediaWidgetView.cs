using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Shapes;
using Chisel.App.ViewModels;
using Chisel.App.Theming;
using Chisel.Core.Media;
using Chisel.Core.Theme;

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
    private const string SpeakerBody = "M3,9 H7 L12,5 V19 L7,15 H3 Z";
    private const string WaveNear = " M14.5,9.5 Q16.5,12 14.5,14.5 L15.8,15.8 Q18.8,12 15.8,8.2 Z";
    private const string WaveFar = " M18,6.5 Q22.5,12 18,17.5 L19.3,18.8 Q25,12 19.3,5.2 Z";
    private const string MutedCross = " M15,9.5 L16,8.5 L18.5,11 L21,8.5 L22,9.5 L19.5,12 L22,14.5 L21,15.5 L18.5,13 L16,15.5 L15,14.5 L17.5,12 Z";
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
    private readonly Grid _buttons;
    private readonly Grid _volumeRow;
    private readonly Button _mute;
    private readonly Path _muteIcon;
    private readonly VolumeSlider _volumeSlider;
    private VolumeViewModel? _volume;
    private readonly ContentControl _body = new();
    private readonly TextBlock _feedback;
    private readonly DispatcherTimer _feedbackTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _wheelTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private WidgetFrame? _frame;
    private byte[]? _shownBytes;
    private Bitmap? _bitmap;
    private readonly AlbumGradientBackground _gradient = new() { Name = "MediaGradient", IsVisible = false, Margin = new Thickness(-16, -12), CornerRadius = new CornerRadius(0, 0, 12, 12) };
    private readonly List<(TextBlock Block, bool Dim)> _texts = [];
    private readonly List<Path> _icons = [];
    private MainViewModel? _main;
    private MediaBackgroundViewModel? _background;

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
        _icons.Add(_playPauseIcon);
        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(-4, 0, 0, 0) };
        buttonRow.Children.Add(_previous);
        buttonRow.Children.Add(_playPause);
        buttonRow.Children.Add(_next);
        _bars = new SpectrumBars { Name = "MediaBars", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_bars, 1);
        var buttons = _buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        buttons.Children.Add(buttonRow);
        buttons.Children.Add(_bars);

        _muteIcon = new Path { Data = Geometry.Parse(SpeakerBody), Stretch = Stretch.Uniform };
        _muteIcon.Bind(Shape.FillProperty, this.GetResourceObservable("CurrentBlockOutlineBrush"));
        _icons.Add(_muteIcon);
        _mute = new Button { Name = "MediaMute", Content = _muteIcon };
        _mute.Classes.Add("mediaButton");
        _volumeSlider = new VolumeSlider { Name = "MediaVolumeSlider", VerticalAlignment = VerticalAlignment.Center };
        _volumeSlider.Dragged += level => _volume?.DragTo(level);
        _volumeSlider.DragCompleted += () => _volume?.CompleteDrag();
        _mute.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => _volume?.ToggleMute());
        Grid.SetColumn(_volumeSlider, 1);
        _volumeRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 4, Margin = new Thickness(-4, 0, 0, 0) };
        _volumeRow.Children.Add(_mute);
        _volumeRow.Children.Add(_volumeSlider);

        var details = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(_title);
        details.Children.Add(_artist);
        details.Children.Add(_album);
        details.Children.Add(_source);
        details.Children.Add(progress);
        details.Children.Add(buttons);
        details.Children.Add(_volumeRow);

        _player = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(details, 1);
        _player.Children.Add(_artBox);
        _player.Children.Add(details);

        // A small quiet percentage in the corner for wheel changes while the volume row is hidden; it overlays, so nothing shifts.
        _feedback = Text("", 11, dim: true, name: "MediaVolumeFeedback");
        _feedback.HorizontalAlignment = HorizontalAlignment.Right;
        _feedback.VerticalAlignment = VerticalAlignment.Bottom;
        _feedback.IsHitTestVisible = false;
        _feedback.IsVisible = false;
        _feedbackTimer.Tick += (_, _) => HideFeedback();
        _wheelTimer.Tick += (_, _) =>
        {
            _wheelTimer.Stop();
            _volume?.CompleteDrag();
        };
        Content = new Grid { Children = { _gradient, _body, _feedback } };
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
            _volume = main.Volume;
            _volume.PropertyChanged += OnVolumeChanged;
            _volume.ScrollFeedback += OnScrollFeedback;
            ShowVolume();
            _main = main;
            _background = main.MediaBackground;
            _background.PropertyChanged += OnBackgroundChanged;
            main.Layout.PropertyChanged += OnLayoutChanged;
        }
        // The wheel works anywhere over the card, header included, so it listens on the frame around this view.
        _frame = this.FindAncestorOfType<WidgetFrame>();
        if (_frame is not null)
        {
            _frame.PointerWheelChanged += OnWheel;
        }
        Refresh();
        ApplyBackground(fade: false);
    }

    // The gradient fills this view, which sits below the header; with no header it fills the whole card.
    protected override void OnHeaderShownChanged() =>
        _gradient.CornerRadius = HeaderShown ? new CornerRadius(0, 0, 12, 12) : new CornerRadius(12);

    private void OnBackgroundChanged(object? sender, PropertyChangedEventArgs e) =>
        ApplyBackground(fade: e.PropertyName == nameof(MediaBackgroundViewModel.Version));

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutViewModel.IsEditing))
        {
            ApplyBackground(fade: false);
        }
    }

    // Brings the gradient in line with the view model: shown or not, its colors (cross-fading when the cover changed and the
    // widget is animating), whether it drifts, and the text and icon colors that keep it readable.
    private void ApplyBackground(bool fade)
    {
        if (_background is not { } background || _main is not { } main)
        {
            return;
        }
        var on = background.IsOn;
        _gradient.IsVisible = on;
        if (on)
        {
            var canFade = fade && _settings?.AnimateGradient != false && IsEffectivelyVisible;
            _gradient.SetColors(background.Colors, canFade);
            _gradient.Animate = background.ShouldAnimate && !main.Layout.IsEditing;
        }
        else
        {
            _gradient.Animate = false;
            _gradient.Clear();
        }
        ApplyForeground(on ? background.Mode : null);
    }

    // On the gradient the text and icons use the theme mode's text colors (the ones the gradient is adjusted to keep readable);
    // off, they go back to the normal card colors.
    private void ApplyForeground(ThemeDefinition? mode)
    {
        if (mode is not null)
        {
            var primary = mode.Text.ToBrush();
            var secondary = AlbumGradient.SecondaryText(mode).ToBrush();
            foreach (var (block, dim) in _texts)
            {
                block.Foreground = dim ? secondary : primary;
            }
            foreach (var icon in _icons)
            {
                icon.Fill = primary;
            }
            var wash = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(mode.Text.ToColor(0x40));
            _track.Background = wash;
            _volumeSlider.TrackOverride = wash;
            return;
        }
        foreach (var (block, dim) in _texts)
        {
            block.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(dim ? "DimTextBrush" : "CurrentBlockOutlineBrush"));
        }
        foreach (var icon in _icons)
        {
            icon.Bind(Shape.FillProperty, this.GetResourceObservable("CurrentBlockOutlineBrush"));
        }
        _track.Bind(Border.BackgroundProperty, this.GetResourceObservable("GridLineBrush"));
        _volumeSlider.TrackOverride = null;
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_volume?.ScrollBy(e.Delta.Y) == true)
        {
            e.Handled = true;
            _wheelTimer.Stop();
            _wheelTimer.Start();
        }
    }

    // With the volume row showing, the slider is the feedback; without it, a percentage shows for about a second.
    private void OnScrollFeedback(object? sender, double level)
    {
        if (_settings?.ShowVolume != false)
        {
            return;
        }
        _feedback.Text = VolumeMath.Percent(level);
        _feedback.IsVisible = true;
        _feedbackTimer.Stop();
        _feedbackTimer.Start();
    }

    private void HideFeedback()
    {
        _feedbackTimer.Stop();
        _feedback.IsVisible = false;
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
        if (_volume is not null)
        {
            _volume.PropertyChanged -= OnVolumeChanged;
            _volume.ScrollFeedback -= OnScrollFeedback;
            _wheelTimer.Stop();
            _volume.CompleteDrag();
            _volume = null;
        }
        if (_frame is not null)
        {
            _frame.PointerWheelChanged -= OnWheel;
            _frame = null;
        }
        if (_background is not null)
        {
            _background.PropertyChanged -= OnBackgroundChanged;
            _background = null;
        }
        if (_main is not null)
        {
            _main.Layout.PropertyChanged -= OnLayoutChanged;
            _main = null;
        }
        _gradient.Animate = false;
        _gradient.Clear();
        HideFeedback();
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
        else if (_settings is not null && e.PropertyName == nameof(MediaSettingsViewModel.ShowVolume))
        {
            ShowVolume();
            FitToRoom();
        }
    }

    private void OnVolumeChanged(object? sender, PropertyChangedEventArgs e) => ShowVolume();

    // Brings the volume row in line with the view model: icon, thumb (unless it is being dragged), tooltips, enabled and visible.
    private void ShowVolume()
    {
        if (_volume is null || _settings is null)
        {
            return;
        }
        _volumeRow.IsVisible = _settings.ShowVolume;
        _volumeRow.IsEnabled = _volume.IsAvailable;
        _volumeRow.Opacity = _volume.IsAvailable ? 1 : 0.35;
        _volumeSlider.Value = _volume.IsMuted ? 0 : _volume.Level;
        _muteIcon.Data = Geometry.Parse(_volume.Icon switch
        {
            VolumeIcon.Muted => SpeakerBody + MutedCross,
            VolumeIcon.Low => SpeakerBody + WaveNear,
            VolumeIcon.Medium => SpeakerBody + WaveFar,
            _ => SpeakerBody + WaveNear + WaveFar,
        });
        ToolTip.SetTip(_mute, _volume.Tip);
        ToolTip.SetTip(_volumeSlider, _volume.Tip);
    }

    private TextBlock Text(string text, double size, bool dim = false, string? name = null, FontWeight weight = FontWeight.Normal)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight, Name = name, TextTrimming = TextTrimming.CharacterEllipsis };
        block.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(dim ? "DimTextBrush" : "CurrentBlockOutlineBrush"));
        _texts.Add((block, dim));
        return block;
    }

    private Button IconButton(string name, string icon, string tip)
    {
        var path = new Path { Data = Geometry.Parse(icon), Stretch = Stretch.Uniform };
        path.Bind(Shape.FillProperty, this.GetResourceObservable("CurrentBlockOutlineBrush"));
        _icons.Add(path);
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
            _body.Content = _nothing;
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
        _body.Content = _player;
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
        // Short cards: the volume shares the buttons' row (and the audio bars give way); otherwise it has a row of its own.
        var beside = Bounds.Height > 0 && plan.VolumeBesideButtons && _settings?.ShowVolume != false;
        if (beside && _volumeRow.Parent != _buttons)
        {
            ((Panel)_volumeRow.Parent!).Children.Remove(_volumeRow);
            Grid.SetColumn(_volumeRow, 1);
            _buttons.Children.Add(_volumeRow);
        }
        else if (!beside && _volumeRow.Parent == _buttons)
        {
            _buttons.Children.Remove(_volumeRow);
            Grid.SetColumn(_volumeRow, 0);
            ((Panel)_player.Children[1]).Children.Add(_volumeRow);
        }
        _bars.IsVisible = !(beside && _volumeRow.IsVisible);
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
