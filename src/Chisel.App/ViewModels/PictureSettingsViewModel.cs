using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Chisel.Core.Layout;
using Chisel.Core.Pictures;
using Chisel.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// One picture frame on the dashboard, as the settings page lists it.
public sealed record FrameChoice(string Id, string Title);

// One picture in the selected frame's list: its thumbnail, its name, and what moves or removes it.
public sealed class PictureRow
{
    public PictureRow(string fileName, string thumbnailPath, bool canMoveUp, bool canMoveDown, Action remove, Action moveUp, Action moveDown)
    {
        FileName = fileName;
        ThumbnailPath = thumbnailPath;
        CanMoveUp = canMoveUp;
        CanMoveDown = canMoveDown;
        RemoveCommand = new RelayCommand(remove);
        MoveUpCommand = new RelayCommand(moveUp, () => canMoveUp);
        MoveDownCommand = new RelayCommand(moveDown, () => canMoveDown);
    }

    public string FileName { get; }
    public string ThumbnailPath { get; }
    public bool CanMoveUp { get; }
    public bool CanMoveDown { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
}

// The Picture frame page in the Widgets settings: the two options for every frame (original files, dropping several
// pictures) and, for the frame picked in the list, its fit, slideshow options and pictures. Saved as soon as it changes.
public sealed partial class PictureSettingsViewModel : ViewModelBase
{
    private const int CustomIntervalSlot = 8;

    private readonly IUserSettingsRepository? _settings;
    private readonly LayoutViewModel _layout;
    private readonly PictureCatalog? _catalog;
    private bool _syncing;

    public PictureSettingsViewModel(IUserSettingsRepository? settings, LayoutViewModel layout, PictureCatalog? catalog)
    {
        _settings = settings;
        _layout = layout;
        _catalog = catalog;
        var saved = settings?.Get();
        _syncing = true;
        KeepOriginals = saved?.PictureKeepOriginals ?? true;
        MultiDropIndex = (int)(saved?.PictureMultiDrop ?? MultiDropMode.OneSlideshow);
        _syncing = false;
        _layout.PropertyChanged += OnLayoutChanged;
        RefreshFrames();
    }

    // ----- for every picture frame

    [ObservableProperty]
    public partial bool KeepOriginals { get; set; }

    // 0 One slideshow, 1 One frame per picture.
    [ObservableProperty]
    public partial int MultiDropIndex { get; set; }

    partial void OnKeepOriginalsChanged(bool value)
    {
        if (!_syncing && _settings?.Get() is { } current && current.PictureKeepOriginals != value)
        {
            _settings.Save(current.WithPictureKeepOriginals(value));
        }
    }

    partial void OnMultiDropIndexChanged(int value)
    {
        var mode = value == 1 ? MultiDropMode.OneFramePerPicture : MultiDropMode.OneSlideshow;
        if (!_syncing && _settings?.Get() is { } current && current.PictureMultiDrop != mode)
        {
            _settings.Save(current.WithPictureMultiDrop(mode));
        }
    }

    // ----- one frame

    public ObservableCollection<FrameChoice> Frames { get; } = new();

    public bool HasFrames => Frames.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial FrameChoice? SelectedFrame { get; set; }

    public bool HasSelection => SelectedFrame is not null;

    partial void OnSelectedFrameChanged(FrameChoice? value) => LoadSelected();

    public IReadOnlyList<string> IntervalChoices { get; } =
        SlideshowInterval.Presets.Select(SlideshowInterval.Format).Append("Custom").ToList();

    // 0 Fill, 1 Fit.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFit))]
    public partial int FitIndex { get; set; }

    public bool IsFit => FitIndex == 1;

    [ObservableProperty]
    public partial bool BlurFill { get; set; }

    // 0 Manual, 1 Cycle.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCycle))]
    public partial int ModeIndex { get; set; }

    public bool IsCycle => ModeIndex == 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomInterval))]
    public partial int IntervalIndex { get; set; }

    public bool IsCustomInterval => IntervalIndex == CustomIntervalSlot;

    [ObservableProperty]
    public partial string CustomIntervalText { get; set; } = "";

    [ObservableProperty]
    public partial bool CustomIntervalInvalid { get; private set; }

    // 0 In order, 1 Shuffle.
    [ObservableProperty]
    public partial int OrderIndex { get; set; }

    // 0 Fade, 1 None.
    [ObservableProperty]
    public partial int TransitionIndex { get; set; }

    public ObservableCollection<PictureRow> Pictures { get; } = new();

    partial void OnFitIndexChanged(int value) => Change(o => o with { Fit = value == 1 ? FitMode.Fit : FitMode.Fill });

    partial void OnBlurFillChanged(bool value) => Change(o => o with { BlurFill = value });

    partial void OnModeIndexChanged(int value) => Change(o => o with { Mode = value == 1 ? SlideshowMode.Cycle : SlideshowMode.Manual });

    partial void OnOrderIndexChanged(int value) => Change(o => o with { Order = value == 1 ? SlideshowOrder.Shuffle : SlideshowOrder.InOrder });

    partial void OnTransitionIndexChanged(int value) => Change(o => o with { Transition = value == 1 ? SlideshowTransition.None : SlideshowTransition.Fade });

    partial void OnIntervalIndexChanged(int value)
    {
        if (!_syncing && value is >= 0 and < CustomIntervalSlot)
        {
            Change(o => o with { IntervalSeconds = SlideshowInterval.Presets[value] });
        }
    }

    partial void OnCustomIntervalTextChanged(string value)
    {
        if (_syncing)
        {
            return;
        }
        if (SlideshowInterval.TryParse(value) is { } seconds)
        {
            CustomIntervalInvalid = false;
            Change(o => o with { IntervalSeconds = seconds });
        }
        else
        {
            CustomIntervalInvalid = true;
        }
    }

    private void Change(Func<FrameOptions, FrameOptions> change)
    {
        if (_syncing || SelectedFrame is not { } frame)
        {
            return;
        }
        _layout.UpdatePictures(p => p.SetOptions(frame.Id, change(p.Get(frame.Id).Options)));
    }

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        if (e.PropertyName == nameof(LayoutViewModel.Layout))
        {
            RefreshFrames();
        }
        else if (e.PropertyName == nameof(LayoutViewModel.Pictures))
        {
            LoadSelected();
        }
    }

    private void RefreshFrames()
    {
        var ids = _layout.Layout.WidgetIds.Where(id => InstanceIds.TypeOf(id) == WidgetRegistry.PictureFrame).ToList();
        var titles = WidgetTitles.For(ids, id => WidgetRegistry.Get(id)?.Title ?? id);
        var wanted = ids.Select(id => new FrameChoice(id, titles[id])).ToList();
        if (wanted.SequenceEqual(Frames))
        {
            return;
        }
        var keep = SelectedFrame?.Id;
        _syncing = true;
        Frames.Clear();
        foreach (var frame in wanted)
        {
            Frames.Add(frame);
        }
        _syncing = false;
        OnPropertyChanged(nameof(HasFrames));
        var next = Frames.FirstOrDefault(f => f.Id == keep) ?? Frames.FirstOrDefault();
        if (Equals(next, SelectedFrame))
        {
            LoadSelected();
        }
        else
        {
            SelectedFrame = next;
        }
    }

    // Shows the selected frame's options and pictures (without writing them back).
    private void LoadSelected()
    {
        var wasSyncing = _syncing;
        _syncing = true;
        try
        {
            Pictures.Clear();
            if (SelectedFrame is not { } frame)
            {
                return;
            }
            var content = _layout.Pictures.Get(frame.Id);
            var o = content.Options;
            FitIndex = o.Fit == FitMode.Fit ? 1 : 0;
            BlurFill = o.BlurFill;
            ModeIndex = o.Mode == SlideshowMode.Cycle ? 1 : 0;
            OrderIndex = o.Order == SlideshowOrder.Shuffle ? 1 : 0;
            TransitionIndex = o.Transition == SlideshowTransition.None ? 1 : 0;
            var preset = SlideshowInterval.Presets.ToList().IndexOf(o.IntervalSeconds);
            IntervalIndex = preset >= 0 ? preset : CustomIntervalSlot;
            CustomIntervalText = preset >= 0 ? "" : SlideshowInterval.Format(o.IntervalSeconds);
            CustomIntervalInvalid = false;

            for (var i = 0; i < content.Pictures.Count; i++)
            {
                var index = i;
                if (_catalog?.Get(content.Pictures[i].AssetId) is not { } asset)
                {
                    continue;
                }
                Pictures.Add(new PictureRow(asset.FileName, _catalog.PathOf(asset), index > 0, index < content.Pictures.Count - 1,
                    () => _layout.UpdatePictures(p => p.RemovePicture(frame.Id, index)),
                    () => _layout.UpdatePictures(p => p.Move(frame.Id, index, -1)),
                    () => _layout.UpdatePictures(p => p.Move(frame.Id, index, 1))));
            }
        }
        finally
        {
            _syncing = wasSyncing;
        }
    }
}
