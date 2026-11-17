using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Chisel.App.Controls;
using Chisel.App.ViewModels;
using Chisel.App.Views.Widgets;

namespace Chisel.App.Views;

// Builds the window's columns and widget cards from a LayoutViewModel, and rebuilds them whenever its
// layout changes. Each widget's card (and so its view and state) is created once and reused.
// Each column shares its height between its cards by weight. If the window is too short for the cards'
// minimum heights, that column scrolls instead of cutting cards off.
public sealed class LayoutHost : Decorator
{
    public const double Gap = 16;

    public static readonly StyledProperty<LayoutViewModel?> LayoutProperty =
        AvaloniaProperty.Register<LayoutHost, LayoutViewModel?>(nameof(Layout));

    private readonly Dictionary<string, WidgetFrame> _frames = new();
    private readonly WeightedStackPanel _columns = new() { Orientation = Orientation.Horizontal, Spacing = Gap };

    public LayoutHost()
    {
        Child = _columns;
    }

    public LayoutViewModel? Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    // The cards currently shown, by column.
    public IReadOnlyList<IReadOnlyList<WidgetFrame>> Frames =>
        ColumnPanels.Select(c => (IReadOnlyList<WidgetFrame>)c.Children.OfType<WidgetFrame>().ToList()).ToList();

    private IEnumerable<WeightedStackPanel> ColumnPanels =>
        _columns.Children.OfType<ScrollViewer>().Select(s => s.Content).OfType<WeightedStackPanel>();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayoutProperty)
        {
            if (change.OldValue is LayoutViewModel old)
            {
                old.PropertyChanged -= OnLayoutChanged;
            }
            if (change.NewValue is LayoutViewModel layout)
            {
                layout.PropertyChanged += OnLayoutChanged;
            }
            Rebuild();
        }
    }

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutViewModel.Columns))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        // Take the reused cards out of their old columns first, so each can join its new one.
        foreach (var column in ColumnPanels)
        {
            column.Children.Clear();
        }
        _columns.Children.Clear();

        if (Layout is null)
        {
            return;
        }

        foreach (var column in Layout.Columns)
        {
            var panel = new WeightedStackPanel { Orientation = Orientation.Vertical, Spacing = Gap };
            panel.Classes.Add("layoutColumn");
            foreach (var slot in column.Widgets)
            {
                var frame = Frame(slot.Widget);
                WeightedStackPanel.SetWeight(frame, slot.HeightWeight);
                panel.Children.Add(frame);
            }

            var minimum = column.Widgets.Sum(w => w.MinHeight) + Gap * (column.Widgets.Count - 1);
            var scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = panel,
            };
            scroller.Classes.Add("layoutColumnScroller");
            // A scroller measures its content with unlimited height, so give the column a real height:
            // the visible height, or the cards' minimums when those don't fit.
            panel.Height = minimum;
            scroller.SizeChanged += (_, e) => panel.Height = Math.Max(e.NewSize.Height, minimum);
            WeightedStackPanel.SetWeight(scroller, column.WidthWeight);
            _columns.Children.Add(scroller);
        }
    }

    private WidgetFrame Frame(WidgetDefinition widget)
    {
        if (!_frames.TryGetValue(widget.Id, out var frame))
        {
            var view = (WidgetView)Activator.CreateInstance(widget.ViewType)!;
            frame = new WidgetFrame(widget, view);
            _frames[widget.Id] = frame;
        }
        return frame;
    }
}
