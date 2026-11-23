using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
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
    private readonly List<(Button Card, ClockFaceItem Face)> _customCards = [];
    private ClockSettingsViewModel? _settings;
    private ClockFacesViewModel? _faces;

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
            _faces = main.ClockFaces;
            _faces.Custom.CollectionChanged += OnCustomChanged;
            _faces.PropertyChanged += OnFacesChanged;
            BuildGallery();
            BuildCustomGallery();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_settings is not null)
        {
            _settings.PropertyChanged -= OnSettingsChanged;
            _settings = null;
        }
        if (_faces is not null)
        {
            _faces.Custom.CollectionChanged -= OnCustomChanged;
            _faces.PropertyChanged -= OnFacesChanged;
            _faces = null;
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
        for (var i = 0; i < ClockPresets.All.Count; i++)
        {
            var preset = ClockPresets.All[i];
            var index = i;
            var image = new Image { Name = "ClockPresetThumb_" + preset.Id, Width = ThumbnailSize, Height = ThumbnailSize, Source = Thumbnail(preset) };
            var caption = new TextBlock { Text = preset.Title, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(image);
            content.Children.Add(caption);
            var card = new Button { Name = "ClockPreset_" + preset.Id, Content = content, Padding = new Thickness(4, 3), Margin = new Thickness(0, 0, 2, 0), HorizontalContentAlignment = HorizontalAlignment.Center };
            card.Classes.Add("settingsTab");
            card.Click += (_, _) =>
            {
                // Through the face list, so the built-in face is the selected one (a built-in face has no rename or delete).
                if (_faces is { } faces && index < faces.BuiltIn.Count)
                {
                    faces.ApplyCommand.Execute(faces.BuiltIn[index]);
                }
                else
                {
                    settings.PresetIndex = index;
                }
            };
            _cards.Add(card);
            gallery.Children.Add(card);
        }
        MarkCurrent();
    }

    private void OnCustomChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => BuildCustomGallery();

    private void OnFacesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClockFacesViewModel.Selected))
        {
            MarkCustom();
        }
    }

    // One card per saved face: its own thumbnail (in its own colors) and name. Clicking one applies it and selects it for the
    // buttons below; the one the widget wears now is marked.
    private void BuildCustomGallery()
    {
        var gallery = this.FindControl<WrapPanel>("ClockCustomGallery")!;
        gallery.Children.Clear();
        _customCards.Clear();
        if (_faces is not { } faces)
        {
            return;
        }
        foreach (var face in faces.Custom)
        {
            var image = new Image { Width = ThumbnailSize, Height = ThumbnailSize, Source = ClockThumbnail.Render(face.Style with { Alignment = ClockAlignment.Center, ShowSeconds = false }) };
            var caption = new TextBlock { Text = face.Title, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 64, TextTrimming = TextTrimming.CharacterEllipsis };
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(image);
            content.Children.Add(caption);
            var card = new Button { Name = "ClockCustomFace_" + face.Title, Content = content, Padding = new Thickness(4, 3), Margin = new Thickness(0, 0, 2, 0), HorizontalContentAlignment = HorizontalAlignment.Center };
            Avalonia.Automation.AutomationProperties.SetName(card, "Face " + face.Title);
            card.Classes.Add("settingsTab");
            var target = face;
            card.Click += (_, _) => faces.ApplyCommand.Execute(target);
            _customCards.Add((card, face));
            gallery.Children.Add(card);
        }
        MarkCustom();
    }

    private void MarkCustom()
    {
        foreach (var (card, face) in _customCards)
        {
            card.Classes.Set("selected", ReferenceEquals(face, _faces?.Selected));
        }
    }

    private async void OnCopyFace(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_faces is { Selected: { } face } faces && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(faces.Export(face));
        }
    }

    private async void OnPasteFace(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_faces is { } faces && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            faces.Import(await clipboard.TryGetTextAsync());
        }
    }

    // A face's preview is the face as defined (its own colors, or the theme's): never the colors chosen for the widget now, which
    // belong to the working style and would make every preview look like whatever was picked last.
    private static Bitmap Thumbnail(ClockPreset preset) =>
        ClockThumbnail.Render(preset.Style with { Alignment = ClockAlignment.Center, ShowSeconds = false });

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
    }
}
