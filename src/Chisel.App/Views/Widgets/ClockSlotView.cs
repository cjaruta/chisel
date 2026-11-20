using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Chisel.App.ViewModels;
using Chisel.Core.Clock;

namespace Chisel.App.Views.Widgets;

// One slot of the clock widget: the tool on screen, a few dots on the right that show the others, and the mouse wheel to move
// between them. Like the media widget's wheel, it only acts when it can (a scrolling slot with more than one tool, the widget on
// screen and not being edited) and then marks the event handled; otherwise the wheel passes on, so a list inside a tool still scrolls
// first. The wheel never changes a value in a tool. Right-click opens a small menu to jump to a tool. The dots show while the
// pointer is over the slot, and stay while a tool that is off screen is running (that dot is accent-coloured with its time).
public sealed class ClockSlotView : Grid
{
    private readonly ClockSlotViewModel _slot;
    private readonly MainViewModel _main;
    private readonly SlideHost _host = new();
    private readonly StackPanel _dots = new()
    {
        Name = "ClockSlotDots", Orientation = Orientation.Vertical, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), IsHitTestVisible = false, IsVisible = false,
    };
    private readonly Dictionary<ClockTool, BuiltTool> _built = [];
    private readonly ContextMenu _menu = new();
    private double _wheel;
    private bool _hover;

    public ClockSlotView(ClockSlotViewModel slot, MainViewModel main)
    {
        _slot = slot;
        _main = main;
        // Something to hit, so the wheel is heard over a tool that ignores the mouse (the face).
        Background = Brushes.Transparent;
        Children.Add(_host);
        Children.Add(_dots);
        PointerEntered += (_, _) => SetHover(true);
        PointerExited += (_, _) => SetHover(false);
        PointerWheelChanged += OnWheel;
        PointerReleased += OnPointerReleased;
        _slot.PropertyChanged += OnSlotChanged;
        _main.Layout.PropertyChanged += OnLayoutChanged;
        _host.Show(ContentOf(_slot.Current), 0);
        UpdateDots();
    }

    // Every tool fits itself to the room this slot has.
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            var scale = ClockTypeScale.For(e.NewSize.Width, e.NewSize.Height);
            foreach (var built in _built.Values)
            {
                built.Rescale(scale);
            }
        }
    }

    public ClockSlotViewModel Slot => _slot;

    // The right-click menu (built when it opens).
    public ContextMenu JumpMenu => _menu;

    public ClockTool Current => _slot.Current;

    // Lets go of the view models and the tool views.
    public void Dispose()
    {
        _slot.PropertyChanged -= OnSlotChanged;
        _main.Layout.PropertyChanged -= OnLayoutChanged;
        foreach (var built in _built.Values)
        {
            built.Dispose();
        }
        _built.Clear();
    }

    private Control ContentOf(ClockTool tool)
    {
        if (!_built.TryGetValue(tool, out var built))
        {
            _built[tool] = built = ClockToolViews.Build(tool, _main, this);
            if (Bounds.Width > 0 && Bounds.Height > 0)
            {
                built.Rescale(ClockTypeScale.For(Bounds.Width, Bounds.Height));
            }
        }
        return built.Control;
    }

    private void OnSlotChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ClockSlotViewModel.Index):
                _host.Show(ContentOf(_slot.Current), _slot.LastStep);
                UpdateDots();
                break;
            case nameof(ClockSlotViewModel.Dots):
            case nameof(ClockSlotViewModel.HasActiveOffscreen):
                UpdateDots();
                break;
        }
    }

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutViewModel.IsEditing))
        {
            UpdateDots();
        }
    }

    private void SetHover(bool hover)
    {
        _hover = hover;
        UpdateDots();
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!IsEffectivelyVisible || _main.Layout.IsEditing || !_slot.CanCycle)
        {
            return;
        }
        // A notch is one unit; a smooth touchpad sends fractions that add up to one.
        _wheel += e.Delta.Y;
        if (Math.Abs(_wheel) >= 1)
        {
            _slot.Cycle(ClockSlotLayout.StepOf(_wheel));
            _wheel = 0;
        }
        e.Handled = true;
    }

    // Right-click: a small menu to jump to any tool of a scrolling slot. A pinned slot, or one being edited, has none.
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right || !_slot.CanCycle || _main.Layout.IsEditing)
        {
            return;
        }
        _menu.Items.Clear();
        foreach (var tool in _slot.Tools)
        {
            var item = new MenuItem { Name = "ClockJump_" + tool, Header = Title(tool), ToggleType = MenuItemToggleType.Radio, IsChecked = tool == _slot.Current };
            var target = tool;
            item.Click += (_, _) => _slot.Select(target);
            _menu.Items.Add(item);
        }
        _menu.Open(this);
        e.Handled = true;
    }

    private static string Title(ClockTool tool) => tool == ClockTool.Empty ? "Blank" : tool.ToString();

    // The dots: only while the pointer is over the slot or something off screen is running, never while editing the layout.
    private void UpdateDots()
    {
        var show = _slot.ShowDots && !_main.Layout.IsEditing && (_hover || _slot.HasActiveOffscreen);
        _dots.IsVisible = show;
        _dots.Children.Clear();
        if (!show)
        {
            return;
        }
        foreach (var dot in _slot.Dots)
        {
            var key = dot.IsActive ? "AccentBrush" : dot.IsCurrent ? "TextBrush" : "DimTextBrush";
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
            if (dot.IsActive && !dot.IsCurrent && dot.Text.Length > 0)
            {
                row.Children.Add(new TextBlock { Text = dot.Text, FontSize = 10, Foreground = Theming.ThemeService.Brush(this, "AccentBrush"), VerticalAlignment = VerticalAlignment.Center });
            }
            row.Children.Add(new Ellipse
            {
                Name = "ClockDot_" + dot.Tool, Width = 6, Height = 6, Fill = Theming.ThemeService.Brush(this, key),
                Opacity = dot.IsCurrent || dot.IsActive ? 1 : 0.55, VerticalAlignment = VerticalAlignment.Center,
            });
            _dots.Children.Add(row);
        }
    }
}
