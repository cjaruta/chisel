using System;
using System.Collections.Generic;
using System.Linq;
using Chisel.Core.Layout;
using Chisel.Core.Pictures;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chisel.App.ViewModels;

// One card in a column of the shown layout: a widget, or a tabbed group (GroupMembers, in tab order, and the
// active tab). Widget is the card's own widget (see WidgetPlacement.WidgetId); a group always shows its header.
public sealed record WidgetSlot(
    WidgetDefinition Widget, double HeightWeight, bool ShowHeader,
    IReadOnlyList<WidgetDefinition>? GroupMembers = null, int ActiveIndex = 0,
    GroupViewMode ViewMode = GroupViewMode.Tabs, IReadOnlyList<double>? PaneWeights = null)
{
    public string Id => Widget.Id;
    public string Title => Widget.Title;
    public bool IsGroup => GroupMembers is { Count: > 1 };

    // The widgets on the card, in tab order.
    public IReadOnlyList<WidgetDefinition> Members => IsGroup ? GroupMembers! : new[] { Widget };

    // The widget whose body is shown.
    public WidgetDefinition ActiveWidget => Members[System.Math.Clamp(ActiveIndex, 0, Members.Count - 1)];

    // A group fits every member: the largest minimum of each (see WidgetGroupSize).
    public double MinHeight => WidgetGroupSize.Minimum(Members.Select(m => (m.MinWidth, m.MinHeight))).Height;
    public double MinWidth => WidgetGroupSize.Minimum(Members.Select(m => (m.MinWidth, m.MinHeight))).Width;

    // What the card needs to show its members side by side: every pane's minimum plus the gaps.
    public double SplitMinWidth => GroupSplit.MinimumWidth(Members.Select(m => m.MinWidth));
}

// Where a drop of pictures goes: into a gap between cards, a new column, or onto a card's middle.
public enum PictureDropKind { Gap, NewColumn, Card }

// The frames a drop of pictures fills, in order, and whether the drop made them (false: it went into a frame that was there).
public sealed record PictureDropPlacement(IReadOnlyList<string> FrameIds, bool CreatedFrames);

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
    private readonly PictureCatalog? _pictureCatalog;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Columns), nameof(TrayWidgets), nameof(HasHiddenWidgets), nameof(CanRemoveWidgets))]
    public partial WidgetLayout Layout { get; private set; }

    // The widgets that are not on the dashboard, in registry order: what the tray lists.
    // A widget that allows several instances is always offered ("Add" makes a new one); any other only while hidden.
    public IReadOnlyList<WidgetDefinition> TrayWidgets =>
        WidgetRegistry.AllTypes.Where(w => w.AllowMultiple || Layout.HiddenWidgetIds.Contains(w.Id)).ToList();

    // Whether the tray has anything to offer.
    public bool HasHiddenWidgets => TrayWidgets.Count > 0;

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
        // An instance is deleted, so there is nothing new in the tray to look at.
        IsTrayOpen = IsTrayOpen || !InstanceIds.IsInstance(widgetId);
    }

    // Shows or hides a widget's title (working copy only; Done saves it, Cancel and Reset put it back).
    public void ToggleHeader(string widgetId)
    {
        if (!IsEditing || Layout.Find(widgetId) is not { } at)
        {
            return;
        }
        var placement = Layout.Columns[at.Column].Widgets[at.Index];
        if (placement.IsGroup)
        {
            return;
        }
        var current = WidgetHeaders.EffectiveShowHeader(placement, WidgetRegistry.Get(widgetId)!.DefaultShowHeader);
        Layout = Layout.SetShowHeader(widgetId, !current);
    }

    // Makes a tab the card's active widget. Outside edit mode it is saved at once (the choice is part of the
    // layout, so it is there at the next start); in edit mode it is part of the working copy, like everything else.
    public void SelectTab(string widgetId)
    {
        if (Layout.Find(widgetId) is null)
        {
            return;
        }
        var next = Layout.SetActive(widgetId);
        if (ReferenceEquals(next, Layout))
        {
            return;
        }
        Layout = next;
        if (!IsEditing)
        {
            _repository?.Save(Layout);
        }
    }

    public static double MinWidthOf(string widgetId) => WidgetRegistry.Get(widgetId)?.MinWidth ?? LayoutLimits.MinimumColumnWidth;

    // Why the card holding this widget cannot show its members side by side at this width; null when it can.
    public string? SplitRefusal(string widgetId, double cardWidth) =>
        Layout.Find(widgetId) is null ? "Not on the dashboard." : Layout.SetViewMode(widgetId, GroupViewMode.Split, cardWidth, MinWidthOf).Refusal;

    // Switches a group between tabs and split view and returns why it was refused (null when it worked or nothing
    // changed). cardWidth is the card's width in pixels. Outside edit mode it is saved at once; in edit mode it is
    // part of the working copy, like everything else.
    public string? SetViewMode(string widgetId, GroupViewMode mode, double cardWidth)
    {
        if (Layout.Find(widgetId) is null)
        {
            return "Not on the dashboard.";
        }
        var result = Layout.SetViewMode(widgetId, mode, cardWidth, MinWidthOf);
        if (!ReferenceEquals(result.Layout, Layout))
        {
            Layout = result.Layout;
            if (!IsEditing)
            {
                _repository?.Save(Layout);
            }
        }
        return result.Refusal;
    }

    // The layout from when the current pane resize began; null when none is going on.
    private WidgetLayout? _paneResizeStart;

    // Starts dragging a divider between panes (also outside edit mode).
    public void BeginPaneResize() => _paneResizeStart = Layout;

    // Shows the resize live: the pane grows by delta (a fraction of the panes' width; negative shrinks it) against the
    // pane to its right, from the weights the resize started with. Each pane stops at its own minimum fraction.
    public void PreviewPaneResize(string widgetId, double delta, double minimumFractionLeft, double minimumFractionRight)
    {
        if (_paneResizeStart is { } start)
        {
            Layout = start.ResizePanes(widgetId, delta, minimumFractionLeft, minimumFractionRight);
        }
    }

    // Keeps the new widths: saved at once outside edit mode, part of the working copy in edit mode.
    public void CommitPaneResize()
    {
        var changed = _paneResizeStart is { } start && !ReferenceEquals(start, Layout);
        _paneResizeStart = null;
        if (changed && !IsEditing)
        {
            _repository?.Save(Layout);
        }
    }

    // Puts the widths back as they were when the resize began.
    public void CancelPaneResize()
    {
        if (_paneResizeStart is { } start)
        {
            Layout = start;
        }
        _paneResizeStart = null;
    }

    // Moves a widget out of its group, onto its own card right below it. Edit mode only.
    public void Unmerge(string widgetId)
    {
        if (IsEditing && Layout.Find(widgetId) is not null)
        {
            Layout = Layout.Split(widgetId, DefaultHeight(widgetId));
        }
    }

    // Moves a tab to a new place in its group. Edit mode only.
    public void ReorderTab(string widgetId, int index)
    {
        if (IsEditing && Layout.Find(widgetId) is not null)
        {
            Layout = Layout.ReorderMember(widgetId, index);
        }
    }

    // Whether the dragged widget can be merged into the target's card: a widget from the dashboard (not the tray),
    // not onto itself, and within the group limits.
    public bool CanMergeInto(string targetId) =>
        DraggedWidgetId is { } id && !IsDraggingFromTray && Layout.CanCombine(id, targetId);

    // Drops the dragged widget onto another card: it becomes the last tab there and the target's tab stays active.
    // Ends the drag either way; returns whether it merged.
    // cardWidth is the target card's width: a split group takes the widget as another pane when it fits there.
    public bool DropOnWidget(string targetId, double cardWidth = 0)
    {
        if (DraggedWidgetId is not { } id || !CanMergeInto(targetId))
        {
            CancelWidgetDrag();
            return false;
        }
        return Finish(Layout.Combine(id, targetId, cardWidth, MinWidthOf));
    }

    // The quick path from the tray: the widget goes to the bottom of the first column. For a type that allows
    // several instances, widgetId is the type id and a new instance is made.
    public void AddWidgetFromTray(string widgetId)
    {
        if (!IsEditing)
        {
            return;
        }
        if (NewInstanceOf(widgetId) is { } instance)
        {
            widgetId = instance;
        }
        else if (!Layout.HiddenWidgetIds.Contains(widgetId))
        {
            return;
        }
        Layout = Layout.AddWidget(widgetId, 0, Layout.Columns[0].Widgets.Count, DefaultHeight(widgetId));
    }

    // A fresh instance id when the id is a type that allows several instances; null for any other widget.
    private string? NewInstanceOf(string typeId) =>
        !InstanceIds.IsInstance(typeId) && WidgetRegistry.Get(typeId) is { AllowMultiple: true }
            ? InstanceIds.NewInstanceId(typeId, Layout.WidgetIds.Concat(Snapshot?.WidgetIds ?? []))
            : null;

    private static double DefaultHeight(string widgetId) => WidgetRegistry.Get(widgetId)?.DefaultHeight ?? 1;

    public IReadOnlyList<LayoutColumnSlot> Columns => Slots(Layout);

    public static IReadOnlyList<LayoutColumnSlot> Slots(WidgetLayout layout)
    {
        // Titles are numbered when a type has several instances on the dashboard ("Frame", "Frame 2").
        var titles = WidgetTitles.For(layout.WidgetIds, id => WidgetRegistry.Get(id)?.Title ?? id);
        WidgetDefinition Widget(string id)
        {
            var definition = WidgetRegistry.Get(id)!;
            return definition.Title == titles[id] ? definition : definition with { Title = titles[id] };
        }

        WidgetSlot Slot(WidgetPlacement w) => w.IsGroup
            ? new WidgetSlot(Widget(w.WidgetId), w.HeightWeight, true,
                w.MemberIds.Select(Widget).ToList(), w.ActiveIndex,
                w.ViewMode, w.Members!.Select(m => m.WidthWeight).ToList())
            : new WidgetSlot(Widget(w.WidgetId), w.HeightWeight,
                WidgetHeaders.EffectiveShowHeader(w, WidgetRegistry.Get(w.WidgetId)!.DefaultShowHeader));

        return layout.Columns
            .Select(c => new LayoutColumnSlot(c.WidthWeight, c.Widgets.Select(Slot).ToList()))
            .ToList();
    }

    // repository: where the layout is saved; null keeps it in memory only.
    public LayoutViewModel(IWidgetLayoutRepository? repository = null, PictureCatalog? pictures = null)
    {
        _repository = repository;
        _pictureCatalog = pictures;
        Pictures = pictures?.Load() ?? PictureFrameSet.Empty;
        var saved = repository?.Get();
        if (saved is null)
        {
            Layout = WidgetRegistry.DefaultLayout;
            repository?.Save(Layout);
        }
        else
        {
            Layout = WidgetLayoutNormalizer.Normalize(saved, WidgetRegistry.DefaultLayout, WidgetRegistry.Types);
            // A widget the saved layout never had (neither placed nor hidden) was just placed: save that once,
            // so from now on it is remembered like the others.
            var known = saved.WidgetIds.Concat(saved.HiddenWidgetIds).ToHashSet();
            if (Layout.WidgetIds.Any(id => !known.Contains(id)))
            {
                repository!.Save(Layout);
            }
        }
    }

    // Every picture frame's options and pictures. Part of the working copy: in edit mode a change is kept here until
    // Done (Cancel puts the old set back); outside edit mode it is saved at once.
    [ObservableProperty]
    public partial PictureFrameSet Pictures { get; private set; }

    private PictureFrameSet? _picturesSnapshot;

    public void UpdatePictures(Func<PictureFrameSet, PictureFrameSet> change)
    {
        var next = change(Pictures);
        if (ReferenceEquals(next, Pictures))
        {
            return;
        }
        Pictures = next;
        if (!IsEditing && _pictureCatalog is not null)
        {
            Pictures = _pictureCatalog.SaveAndSweep(Pictures, Layout.WidgetIds);
        }
    }

    // Shows a new layout, normalized; the host rebuilds right away. Not saved here.
    public void Apply(WidgetLayout layout) => Layout = WidgetLayoutNormalizer.Normalize(layout, WidgetRegistry.DefaultLayout, WidgetRegistry.Types);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnterEditCommand), nameof(DoneCommand), nameof(CancelCommand), nameof(ResetCommand))]
    public partial bool IsEditing { get; private set; }

    // The layout as it was when edit mode started; null outside edit mode.
    public WidgetLayout? Snapshot { get; private set; }

    [RelayCommand(CanExecute = nameof(CanEnterEdit))]
    private void EnterEdit()
    {
        Snapshot = Layout;
        _picturesSnapshot = Pictures;
        // Opens only when the user hid one of the original widgets, not for the clock, weather or media waiting there by default.
        IsTrayOpen = Layout.HiddenWidgetIds.Any(id => id != WidgetRegistry.Clock && id != WidgetRegistry.Weather && id != WidgetRegistry.Media && id != WidgetRegistry.Calculator);
        IsEditing = true;
    }

    private bool CanEnterEdit() => !IsEditing;

    // Drops already remove emptied columns; this is a safety net so what is saved is exactly what loads.
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Done()
    {
        Layout = Layout.RemoveEmptyColumns();
        _repository?.Save(Layout);
        if (_pictureCatalog is not null)
        {
            // Pictures only frames that are still on the dashboard keep; whatever no frame uses is deleted now, not before.
            Pictures = _pictureCatalog.SaveAndSweep(Pictures, Layout.WidgetIds);
        }
        else
        {
            Pictures = Pictures.PrunedTo(Layout.WidgetIds);
        }
        Exit();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Cancel()
    {
        Layout = Snapshot!;
        Pictures = _picturesSnapshot ?? Pictures;
        Exit();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Reset() => Layout = WidgetRegistry.DefaultLayout;

    private bool CanEdit() => IsEditing;

    private void Exit()
    {
        CancelWidgetDrag();
        CancelResize();
        CancelPaneResize();
        Snapshot = null;
        _picturesSnapshot = null;
        AdjustingFrameId = null;
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
        if (!IsEditing)
        {
            return;
        }
        // A type that allows several instances drags a new instance, made when the drag begins and added only on drop.
        var instance = NewInstanceOf(widgetId);
        if (instance is not null || Layout.HiddenWidgetIds.Contains(widgetId))
        {
            IsDraggingFromTray = true;
            DraggedWidgetId = instance ?? widgetId;
        }
    }

    // Takes away a frame a drop made that ended with no picture: the drop's own, so it is not an undoable edit.
    public void DiscardFrame(string frameId)
    {
        if (Layout.Find(frameId) is null || !Layout.CanRemoveWidget || Pictures.Get(frameId).Pictures.Count > 0)
        {
            return;
        }
        Layout = Layout.RemoveWidget(frameId);
        if (!IsEditing)
        {
            _repository?.Save(Layout);
        }
    }

    // The picture frame whose current picture is being adjusted (panned and zoomed); null when none. Edit mode only.
    [ObservableProperty]
    public partial string? AdjustingFrameId { get; private set; }

    // Asked before adjusting starts: false for a picture that cannot be adjusted (an animated GIF).
    public Func<string, bool>? AdjustGate { get; set; }

    public bool BeginAdjust(string frameId)
    {
        if (!IsEditing || Layout.Find(frameId) is null || AdjustGate?.Invoke(frameId) == false)
        {
            return false;
        }
        AdjustingFrameId = frameId;
        return true;
    }

    public void EndAdjust() => AdjustingFrameId = null;

    // Whether the drag in progress carries pictures from outside the app (it adds frames, or fills one).
    [ObservableProperty]
    public partial bool IsDraggingPictures { get; private set; }

    // Starts a drag of pictures from outside the app. Only in edit mode (a drop outside it is refused). Like a drag from
    // the tray, it carries a new picture frame that is only added when the drop lands.
    public bool BeginPictureDrag()
    {
        if (!IsEditing || NewInstanceOf(WidgetRegistry.PictureFrame) is not { } id)
        {
            return false;
        }
        IsDraggingFromTray = true;
        IsDraggingPictures = true;
        DraggedWidgetId = id;
        return true;
    }

    // Lands a drop of pictures and ends the drag. frameCount is how many new frames the drop makes (at least 1);
    // column and index say where (see PictureDropKind). Onto a card whose shown widget is a picture frame, nothing is
    // added: the pictures go into that frame. Null when no picture drag is going on or the place does not exist.
    public PictureDropPlacement? DropPictures(PictureDropKind kind, int column, int index, int frameCount, double cardWidth = 0)
    {
        if (!IsDraggingPictures)
        {
            return null;
        }
        EndDrag();
        if (column < 0 || column >= Layout.Columns.Count + (kind == PictureDropKind.NewColumn ? 1 : 0)
            || (kind == PictureDropKind.Card && (index < 0 || index >= Layout.Columns[column].Widgets.Count)))
        {
            return null;
        }

        WidgetPlacement? card = kind == PictureDropKind.Card ? Layout.Columns[column].Widgets[index] : null;
        if (card is not null && WidgetRegistry.Get(card.ActiveId)?.TypeId == WidgetRegistry.PictureFrame)
        {
            return new PictureDropPlacement([card.ActiveId], false);
        }

        var ids = new List<string>();
        string NewId()
        {
            var id = InstanceIds.NewInstanceId(WidgetRegistry.PictureFrame, Layout.WidgetIds.Concat(Snapshot?.WidgetIds ?? []).Concat(ids));
            ids.Add(id);
            return id;
        }

        var height = DefaultHeight(WidgetRegistry.PictureFrame);
        var layout = Layout;
        var first = NewId();
        switch (kind)
        {
            case PictureDropKind.Gap:
                layout = layout.AddWidget(first, column, index, height);
                break;
            case PictureDropKind.NewColumn:
                layout = layout.CanAddInNewColumn
                    ? layout.AddWidgetInNewColumn(first, column)
                    : layout.AddWidget(first, Math.Min(column, layout.Columns.Count - 1), layout.Columns[Math.Min(column, layout.Columns.Count - 1)].Widgets.Count, height);
                break;
            default:
                layout = layout.AddWidget(first, column, index + 1, height);
                if (layout.CanCombine(first, card!.WidgetId))
                {
                    layout = layout.Combine(first, card.WidgetId, cardWidth, MinWidthOf);
                }
                break;
        }
        // The rest of a drop of several: one card each, stacked right below the one before (the first, when it joined a card as a tab, goes below that card).
        for (var k = 1; k < Math.Max(1, frameCount); k++)
        {
            var anchorId = k == 1 && card is not null && layout.Find(first) == layout.Find(card.WidgetId) ? card.WidgetId : ids[k - 1];
            var anchor = layout.Find(anchorId)!.Value;
            layout = layout.AddWidget(NewId(), anchor.Column, anchor.Index + 1, height);
        }
        Layout = layout.RemoveEmptyColumns();
        return new PictureDropPlacement(ids, true);
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
        IsDraggingPictures = false;
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
