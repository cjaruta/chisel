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
        Snapshot = null;
        IsEditing = false;
    }
}
