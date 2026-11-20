using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;

namespace Chisel.App.Views.Pages;

public partial class ClockSettingsPage : UserControl
{
    private const int ThumbnailSize = 38;

    private readonly List<Button> _cards = [];
    private ClockSettingsViewModel? _settings;
    private ClockColors _drawnWith;

    public ClockSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is MainViewModel main)
        {
            _settings = main.ClockSettings;
            _settings.PropertyChanged += OnSettingsChanged;
            BuildGallery();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_settings is not null)
        {
            _settings.PropertyChanged -= OnSettingsChanged;
            _settings = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    // One card per preset: its thumbnail and name; the one matching the current look is marked, and clicking one applies it.
    private void BuildGallery()
    {
        var gallery = this.FindControl<WrapPanel>("ClockPresetGallery")!;
        gallery.Children.Clear();
        _cards.Clear();
        if (_settings is not { } settings)
        {
            return;
        }
        _drawnWith = settings.Style.Colors;
        for (var i = 0; i < ClockPresets.All.Count; i++)
        {
            var preset = ClockPresets.All[i];
            var index = i;
            var image = new Image { Name = "ClockPresetThumb_" + preset.Id, Width = ThumbnailSize, Height = ThumbnailSize, Source = Thumbnail(preset, settings) };
            var caption = new TextBlock { Text = preset.Title, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(image);
            content.Children.Add(caption);
            var card = new Button { Name = "ClockPreset_" + preset.Id, Content = content, Padding = new Thickness(4, 3), Margin = new Thickness(0, 0, 2, 0), HorizontalContentAlignment = HorizontalAlignment.Center };
            card.Classes.Add("settingsTab");
            card.Click += (_, _) => settings.PresetIndex = index;
            _cards.Add(card);
            gallery.Children.Add(card);
        }
        MarkCurrent();
    }

    private static Bitmap Thumbnail(ClockPreset preset, ClockSettingsViewModel settings) =>
        ClockThumbnail.Render(preset.Style with { Alignment = ClockAlignment.Center, ShowSeconds = false, Colors = settings.Style.Colors });

    private void MarkCurrent()
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            _cards[i].Classes.Set("selected", _settings?.PresetIndex == i);
        }
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_settings is not { } settings)
        {
            return;
        }
        if (e.PropertyName == nameof(ClockSettingsViewModel.PresetIndex))
        {
            MarkCurrent();
        }
        else if (e.PropertyName == nameof(ClockSettingsViewModel.Style) && settings.Style.Colors != _drawnWith)
        {
            // The thumbnails wear the current colors, so they are drawn again when those change.
            _drawnWith = settings.Style.Colors;
            for (var i = 0; i < _cards.Count; i++)
            {
                if (((StackPanel)_cards[i].Content!).Children[0] is Image image)
                {
                    image.Source = Thumbnail(ClockPresets.All[i], settings);
                }
            }
        }
    }
}
