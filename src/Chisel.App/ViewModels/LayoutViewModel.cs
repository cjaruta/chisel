using System.Collections.Generic;
using System.Linq;
using Chisel.Core.Layout;
using CommunityToolkit.Mvvm.ComponentModel;

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
public partial class LayoutViewModel : ViewModelBase
{
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

    // Shows a new layout, normalized; the host rebuilds right away.
    public void Apply(WidgetLayout layout) => Layout = WidgetLayoutNormalizer.Normalize(layout, WidgetRegistry.DefaultLayout);
}
