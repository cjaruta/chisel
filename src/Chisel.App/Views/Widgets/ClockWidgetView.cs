using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// The clock widget: up to three slots side by side (wide widget) or one above the other (tall widget), each a clock, timer,
// stopwatch, alarm or nothing, set up on the Clock page in settings. The widget holds no controls for that setup and nothing
// sits on or between the slots: just the tools over the card. A widget too small for its slots gives the last one the tools of
// the hidden ones (see ClockSlotLayout). While it is on screen the view follows MainViewModel.ClockSlots; as a hidden tab of a
// group it lets go (and so stops redrawing) until it is shown again. In edit mode the card is inert like every other widget, so
// nothing here competes with dragging it.
public sealed class ClockWidgetView : WidgetView
{
    private static readonly Thickness BodyPadding = new(0, 0, 0, 12);

    private readonly Grid _grid = new() { Name = "ClockSlots" };
    private readonly List<ClockSlotView> _views = [];
    private MainViewModel? _main;
    private bool _following;

    public ClockWidgetView()
    {
        // The heading sits 12px above this view's top (header margin and its spare height). Pad the bottom by the
        // same amount so the face has equal space above and below.
        Padding = BodyPadding;
        Content = _grid;
    }

    // Whether the widget is following the clock (it is while the widget is shown).
    public bool IsFollowingClock => _following;

    // With no title bar the face is centered in the whole card, so the bottom padding goes.
    protected override void OnHeaderShownChanged()
    {
        Padding = HeaderShown ? BodyPadding : new Thickness(0);
        ReportSize();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _main = DataContext as MainViewModel;
        Follow(IsEffectivelyVisible);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Follow(false);
        _main = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_main is not null && (change.Property == IsVisibleProperty || change.Property.Name == nameof(IsEffectivelyVisible)))
        {
            Follow(IsEffectivelyVisible);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ReportSize();
    }

    private void Follow(bool on)
    {
        if (on && _main is { } main && !_following)
        {
            _following = true;
            main.ClockSlots.PropertyChanged += OnSlotsChanged;
            ReportSize();
            Rebuild();
        }
        else if (!on && _following)
        {
            _following = false;
            if (_main is { } m)
            {
                m.ClockSlots.PropertyChanged -= OnSlotsChanged;
                m.ClockSlots.Clear();
            }
            Release();
        }
    }

    // Tells the slots how much room there is (the card less its padding), so they can decide how many fit.
    private void ReportSize()
    {
        if (!_following || _main is not { } main)
        {
            return;
        }
        var width = Bounds.Width - Padding.Left - Padding.Right;
        var height = Bounds.Height - Padding.Top - Padding.Bottom;
        if (width > 0 && height > 0)
        {
            main.ClockSlots.Resize(width, height);
        }
    }

    private void OnSlotsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClockSlotsViewModel.Extents))
        {
            ApplyExtents();
        }
        else if (e.PropertyName is nameof(ClockSlotsViewModel.Slots) or nameof(ClockSlotsViewModel.Horizontal))
        {
            Rebuild();
        }
    }

    // A resize changes how much room each slot has without changing which slots there are: just resize the columns or rows.
    private void ApplyExtents()
    {
        if (_main is not { } main || main.ClockSlots.Slots.Count != _views.Count)
        {
            return;
        }
        var extents = main.ClockSlots.Extents;
        if (main.ClockSlots.Horizontal && _grid.ColumnDefinitions.Count == extents.Count)
        {
            for (var i = 0; i < extents.Count; i++)
            {
                _grid.ColumnDefinitions[i].Width = new GridLength(extents[i], GridUnitType.Star);
            }
        }
        else if (!main.ClockSlots.Horizontal && _grid.RowDefinitions.Count == extents.Count)
        {
            for (var i = 0; i < extents.Count; i++)
            {
                _grid.RowDefinitions[i].Height = new GridLength(extents[i], GridUnitType.Star);
            }
        }
    }

    private void Release()
    {
        foreach (var view in _views)
        {
            view.Dispose();
        }
        _views.Clear();
        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        _grid.ColumnDefinitions.Clear();
    }

    private void Rebuild()
    {
        Release();
        if (_main is not { } main || !_following)
        {
            return;
        }
        var horizontal = main.ClockSlots.Horizontal;
        var index = 0;
        foreach (var slot in main.ClockSlots.Slots)
        {
            var star = new GridLength(slot.Extent, GridUnitType.Star);
            var view = new ClockSlotView(slot, main);
            if (horizontal)
            {
                _grid.ColumnDefinitions.Add(new ColumnDefinition(star));
                Grid.SetColumn(view, index);
            }
            else
            {
                _grid.RowDefinitions.Add(new RowDefinition(star));
                Grid.SetRow(view, index);
            }
            _views.Add(view);
            _grid.Children.Add(view);
            index++;
        }
    }
}
