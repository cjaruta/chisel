using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Chisel.App.ViewModels;
using Chisel.Core.Clock;

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
            ApplyLayout();
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

    // A resize changes how much room each slot has without changing which slots there are: refit the cells in place (the tools
    // on screen keep their state).
    private void ApplyExtents() => ApplyLayout();

    // Gives every slot its cell. A pinned clock is a square (as big as its equal share and the card's height allow), a pinned tool is
    // as big as its content, and a slot that scrolls through several tools keeps its whole equal share (so cycling never moves
    // anything). The rest of the room is divided into equal gaps: one between every two slots and one at each end. Each slot's cell
    // holds half of the gaps beside it, so the wheel and the right-click menu work over the whole cell. A single slot fills the card
    // and a single clock follows the alignment setting inside it. Fonts are sized from the equal share, not from the slot's own
    // size, so a tool never shrinks itself.
    private void ApplyLayout()
    {
        if (_main is not { } main || main.ClockSlots.Slots.Count != _views.Count || _views.Count == 0)
        {
            return;
        }
        var slots = main.ClockSlots.Slots;
        var extents = main.ClockSlots.Extents;
        var horizontal = main.ClockSlots.Horizontal;
        var room = horizontal ? Bounds.Width - Padding.Left - Padding.Right : Bounds.Height - Padding.Top - Padding.Bottom;
        var cross = horizontal ? Bounds.Height - Padding.Top - Padding.Bottom : Bounds.Width - Padding.Left - Padding.Right;
        if (extents.Count != _views.Count || room <= 0 || cross <= 0)
        {
            return;
        }

        var n = _views.Count;
        var content = new double[n];
        for (var i = 0; i < n; i++)
        {
            var cell = extents[i];
            _views[i].Fit(horizontal ? cell : cross, horizontal ? cross : cell);
            if (n == 1)
            {
                content[i] = room - 2 * ClockSlotLayout.OuterMargin;
            }
            else if (IsClock(slots[i]))
            {
                content[i] = Math.Min(cell, cross);
            }
            else if (slots[i].Scrolling)
            {
                content[i] = cell;
            }
            else
            {
                _views[i].Measure(horizontal ? new Size(cell, cross) : new Size(cross, cell));
                var natural = horizontal ? _views[i].DesiredSize.Width : _views[i].DesiredSize.Height;
                content[i] = Math.Min(cell, Math.Max(natural, 1));
            }
        }

        // Equal gaps between the slots and at both ends.
        var gap = n == 1 ? ClockSlotLayout.OuterMargin : Math.Max(0, (room - content.Sum()) / (n + 1));
        for (var i = 0; i < n; i++)
        {
            var before = (i == 0 ? gap : gap / 2);
            var after = (i == n - 1 ? gap : gap / 2);
            var size = n == 1 ? new GridLength(1, GridUnitType.Star) : new GridLength(content[i] + before + after);
            if (horizontal)
            {
                _grid.ColumnDefinitions[i].Width = size;
                _views[i].Margin = new Thickness(before, 0, after, 0);
            }
            else
            {
                _grid.RowDefinitions[i].Height = size;
                _views[i].Margin = new Thickness(0, before, 0, after);
            }
        }
    }

    private static bool IsClock(ClockSlotViewModel slot) => !slot.Scrolling && slot.Tools.Count == 1 && slot.Tools[0] == ClockTool.Clock;

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
        var slots = main.ClockSlots.Slots;
        for (var index = 0; index < slots.Count; index++)
        {
            var view = new ClockSlotView(slots[index], main);
            if (horizontal)
            {
                _grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                Grid.SetColumn(view, index);
            }
            else
            {
                _grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
                Grid.SetRow(view, index);
            }
            _views.Add(view);
            _grid.Children.Add(view);
        }
        ApplyLayout();
    }
}
