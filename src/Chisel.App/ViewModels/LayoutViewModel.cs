using System.Collections.Generic;
using System.Linq;
using Chisel.Core.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// One widget in a column of the shown layout.
public sealed record WidgetSlot(WidgetDefinition Widget, double HeightWeight)
{
    public string Id => Widget.Id;
    public string Title => Widget.Title;
    public double MinHeight => Widget.MinHeight;
}

// One column of the shown layout.
public sealed record LayoutColumnSlot(double WidthWeight, IReadOnlyList<WidgetSlot> Widgets);

// The window's layout. Loads the saved one (or the default, which it then saves), always normalized
// against the widget registry. The layout host is built from Columns and rebuilds when they change.
//
// Edit mode: entering takes a Snapshot; Layout is then the working copy that editing changes (via Apply).
// Done saves the working copy, Cancel puts the snapshot back, Reset swaps in the default (saved only on Done).
public partial class LayoutViewModel : ViewModelBase
{
    private readonly IWidgetLayoutRepository? _repository;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Columns))]
    public partial WidgetLayout Layout { get; private set; }

    public IReadOnlyList<LayoutColumnSlot> Columns => Layout.Columns
        .Select(c => new LayoutColumnSlot(c.WidthWeight,
            c.Widgets.Select(w => new WidgetSlot(WidgetRegistry.Get(w.WidgetId)!, w.HeightWeight)).ToList()))
        .ToList();

    // repository: where the layout is saved; null keeps it in memory only.
    public LayoutViewModel(IWidgetLayoutRepository? repository = null)
    {
        _repository = repository;
        var saved = repository?.Get();
        if (saved is null)
        {
            Layout = WidgetRegistry.DefaultLayout;
            repository?.Save(Layout);
        }
        else
        {
            Layout = WidgetLayoutNormalizer.Normalize(saved, WidgetRegistry.DefaultLayout);
        }
    }

    // Shows a new layout, normalized; the host rebuilds right away. Not saved here.
    public void Apply(WidgetLayout layout) => Layout = WidgetLayoutNormalizer.Normalize(layout, WidgetRegistry.DefaultLayout);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnterEditCommand), nameof(DoneCommand), nameof(CancelCommand), nameof(ResetCommand))]
    public partial bool IsEditing { get; private set; }

    // The layout as it was when edit mode started; null outside edit mode.
    public WidgetLayout? Snapshot { get; private set; }

    [RelayCommand(CanExecute = nameof(CanEnterEdit))]
    private void EnterEdit()
    {
        Snapshot = Layout;
        IsEditing = true;
    }

    private bool CanEnterEdit() => !IsEditing;

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Done()
    {
        _repository?.Save(Layout);
        Exit();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Cancel()
    {
        Layout = Snapshot!;
        Exit();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Reset() => Layout = WidgetRegistry.DefaultLayout;

    private bool CanEdit() => IsEditing;

    private void Exit()
    {
        CancelWidgetDrag();
        CancelResize();
        Snapshot = null;
        IsEditing = false;
    }

    // The widget being dragged in edit mode; null when no drag is going on.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDraggingWidget))]
    public partial string? DraggedWidgetId { get; private set; }

    public bool IsDraggingWidget => DraggedWidgetId is not null;

    // Picks up a widget. Only in edit mode, and only a widget that is in the layout.
    public void BeginWidgetDrag(string widgetId)
    {
        if (IsEditing && Layout.Find(widgetId) is not null)
        {
            DraggedWidgetId = widgetId;
        }
    }

    // Drops the dragged widget into a gap of a column (see WidgetLayout.Move), changing only the working copy.
    // Returns whether the layout changed; dropping onto its own place changes nothing. Ends the drag either way.
    public bool DropWidget(int column, int index)
    {
        if (DraggedWidgetId is not { } id || column < 0 || column >= Layout.Columns.Count)
        {
            CancelWidgetDrag();
            return false;
        }

        var moved = Layout.Move(id, column, index);
        DraggedWidgetId = null;
        if (ReferenceEquals(moved, Layout))
        {
            return false;
        }
        Layout = moved;
        return true;
    }

    // Puts the widget down where it was, leaving the layout as it is.
    public void CancelWidgetDrag() => DraggedWidgetId = null;

    // The layout from when the current resize began; null when no resize is going on.
    private WidgetLayout? _resizeStart;

    [ObservableProperty]
    public partial bool IsResizing { get; private set; }

    // Starts dragging a resize handle. Only in edit mode.
    public void BeginResize()
    {
        if (IsEditing)
        {
            _resizeStart = Layout;
            IsResizing = true;
        }
    }

    // Shows the resize live: the widget grows by delta (a fraction of its column's height, negative shrinks it)
    // against the widget below, from the weights the resize started with. See WidgetLayout.Resize.
    public void PreviewResize(string widgetId, double delta, double minimumFraction)
    {
        if (_resizeStart is { } start)
        {
            Layout = start.Resize(widgetId, delta, minimumFraction);
        }
    }

    // Keeps the resized weights in the working copy.
    public void CommitResize()
    {
        _resizeStart = null;
        IsResizing = false;
    }

    // Puts the weights back as they were when the resize began.
    public void CancelResize()
    {
        if (_resizeStart is { } start)
        {
            Layout = start;
        }
        _resizeStart = null;
        IsResizing = false;
    }
}
