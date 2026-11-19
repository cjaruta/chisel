using System.Collections.Generic;
using System.Linq;
using Chisel.Core.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// One widget in a column of the shown layout.
public sealed record WidgetSlot(WidgetDefinition Widget, double HeightWeight, bool ShowHeader)
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
    [NotifyPropertyChangedFor(nameof(Columns), nameof(TrayWidgets), nameof(HasHiddenWidgets), nameof(CanRemoveWidgets))]
    public partial WidgetLayout Layout { get; private set; }

    // The widgets that are not on the dashboard, in registry order: what the tray lists.
    public IReadOnlyList<WidgetDefinition> TrayWidgets =>
        WidgetRegistry.All.Where(w => Layout.HiddenWidgetIds.Contains(w.Id)).ToList();

    public bool HasHiddenWidgets => Layout.HiddenWidgetIds.Count > 0;

    // False when only one widget is left: it cannot be removed.
    public bool CanRemoveWidgets => Layout.CanRemoveWidget;

    // Whether the tray shows its cards. It opens when entering edit mode with widgets hidden and when one is removed.
    [ObservableProperty]
    public partial bool IsTrayOpen { get; set; }

    [RelayCommand]
    private void ToggleTray() => IsTrayOpen = !IsTrayOpen;

    // Takes a widget off the dashboard (working copy only; its data is untouched). Not the last one.
    public void RemoveWidget(string widgetId)
    {
        if (!IsEditing || Layout.Find(widgetId) is null || !Layout.CanRemoveWidget)
        {
            return;
        }
        Layout = Layout.RemoveWidget(widgetId);
        IsTrayOpen = true;
    }

    // Shows or hides a widget's title (working copy only; Done saves it, Cancel and Reset put it back).
    public void ToggleHeader(string widgetId)
    {
        if (!IsEditing || Layout.Find(widgetId) is not { } at)
        {
            return;
        }
        var current = WidgetHeaders.EffectiveShowHeader(Layout.Columns[at.Column].Widgets[at.Index], WidgetRegistry.Get(widgetId)!.DefaultShowHeader);
        Layout = Layout.SetShowHeader(widgetId, !current);
    }

    // The quick path from the tray: the widget goes to the bottom of the first column.
    public void AddWidgetFromTray(string widgetId)
    {
        if (!IsEditing || !Layout.HiddenWidgetIds.Contains(widgetId))
        {
            return;
        }
        Layout = Layout.AddWidget(widgetId, 0, Layout.Columns[0].Widgets.Count, DefaultHeight(widgetId));
    }

    private static double DefaultHeight(string widgetId) => WidgetRegistry.Get(widgetId)?.DefaultHeight ?? 1;

    public IReadOnlyList<LayoutColumnSlot> Columns => Slots(Layout);

    public static IReadOnlyList<LayoutColumnSlot> Slots(WidgetLayout layout) => layout.Columns
        .Select(c => new LayoutColumnSlot(c.WidthWeight,
            c.Widgets.Select(w => new WidgetSlot(WidgetRegistry.Get(w.WidgetId)!, w.HeightWeight,
                WidgetHeaders.EffectiveShowHeader(w, WidgetRegistry.Get(w.WidgetId)!.DefaultShowHeader))).ToList()))
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
            // A widget the saved layout never had (neither placed nor hidden) was just placed: save that once,
            // so from now on it is remembered like the others.
            var known = saved.WidgetIds.Concat(saved.HiddenWidgetIds).ToHashSet();
            if (Layout.WidgetIds.Any(id => !known.Contains(id)))
            {
                repository!.Save(Layout);
            }
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
        // Opens only when the user hid one of the original widgets, not for the clock or weather waiting there by default.
        IsTrayOpen = Layout.HiddenWidgetIds.Any(id => id != WidgetRegistry.Clock && id != WidgetRegistry.Weather);
        IsEditing = true;
    }

    private bool CanEnterEdit() => !IsEditing;

    // Drops already remove emptied columns; this is a safety net so what is saved is exactly what loads.
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Done()
    {
        Layout = Layout.RemoveEmptyColumns();
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

    // The widget being dragged comes from the tray (it is hidden) rather than from the dashboard.
    [ObservableProperty]
    public partial bool IsDraggingFromTray { get; private set; }

    // Picks a hidden widget up from the tray. Only in edit mode.
    public void BeginTrayDrag(string widgetId)
    {
        if (IsEditing && Layout.HiddenWidgetIds.Contains(widgetId))
        {
            IsDraggingFromTray = true;
            DraggedWidgetId = widgetId;
        }
    }

    // Picks up a widget. Only in edit mode, and only a widget that is in the layout.
    public void BeginWidgetDrag(string widgetId)
    {
        if (IsEditing && Layout.Find(widgetId) is not null)
        {
            IsDraggingFromTray = false;
            DraggedWidgetId = widgetId;
        }
    }

    // Drops the dragged widget into a gap of a column (see WidgetLayout.Move), changing only the working copy.
    // A column the drop leaves empty is removed right away, so the edit view always matches what Done saves.
    // Returns whether the layout changed; dropping onto its own place changes nothing. Ends the drag either way.
    public bool DropWidget(int column, int index)
    {
        if (DraggedWidgetId is not { } id || column < 0 || column >= Layout.Columns.Count)
        {
            CancelWidgetDrag();
            return false;
        }

        return Finish(PreviewDrop(column, index)!);
    }

    // The layout a drop into a gap of a column would give (nothing is changed); null when no drag is going on.
    public WidgetLayout? PreviewDrop(int column, int index)
    {
        if (DraggedWidgetId is not { } id || column < 0 || column >= Layout.Columns.Count)
        {
            return null;
        }
        return IsDraggingFromTray ? Layout.AddWidget(id, column, index, DefaultHeight(id)) : Layout.Move(id, column, index);
    }

    // Ends the drag with the dropped layout, which replaces the working copy first so the host sees the new order
    // (and not the old one) when the drag ends. Returns whether the layout changed.
    private bool Finish(WidgetLayout moved)
    {
        var changed = !ReferenceEquals(moved, Layout);
        if (changed)
        {
            Layout = moved.RemoveEmptyColumns();
        }
        EndDrag();
        return changed;
    }

    // Puts the widget down where it was (or back in the tray), leaving the layout as it is.
    public void CancelWidgetDrag() => EndDrag();

    private void EndDrag()
    {
        DraggedWidgetId = null;
        IsDraggingFromTray = false;
    }

    private bool CanDropInNewColumn(string id) => IsDraggingFromTray ? Layout.CanAddInNewColumn : Layout.CanMoveToNewColumn(id);

    // Drops the dragged widget into a new column at a gap between columns (0 = left edge, Count = right edge),
    // changing only the working copy. See WidgetLayout.MoveToNewColumn. Ends the drag either way.
    public bool DropWidgetInNewColumn(int columnIndex)
    {
        if (DraggedWidgetId is not { } id || !CanDropInNewColumn(id) || columnIndex < 0 || columnIndex > Layout.Columns.Count)
        {
            CancelWidgetDrag();
            return false;
        }

        return Finish(IsDraggingFromTray ? Layout.AddWidgetInNewColumn(id, columnIndex) : Layout.MoveToNewColumn(id, columnIndex));
    }

    // Whether "New column" drop zones should be offered for the widget being dragged, given the width the
    // columns share (gap between columns in pixels): not past LayoutLimits.MaximumColumns, and only while every
    // column could still be LayoutLimits.MinimumColumnWidth wide. A widget alone in its column only moves its
    // column, so it can always go.
    public bool CanOfferNewColumn(double availableWidth, double gap)
    {
        if (DraggedWidgetId is not { } id || !CanDropInNewColumn(id))
        {
            return false;
        }
        if (!IsDraggingFromTray && Layout.Columns[Layout.Find(id)!.Value.Column].Widgets.Count == 1)
        {
            return true;
        }
        var count = Layout.Columns.Count + 1;
        return count * LayoutLimits.MinimumColumnWidth + (count - 1) * gap <= availableWidth;
    }

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

    // Shows a column resize live: the column grows by delta (a fraction of the columns' shared width, negative
    // shrinks it) against the column to its right, from the widths the resize started with.
    // See WidgetLayout.ResizeColumns.
    public void PreviewColumnResize(int columnIndex, double delta, double minimumFraction)
    {
        if (_resizeStart is { } start)
        {
            Layout = start.ResizeColumns(columnIndex, delta, minimumFraction);
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
