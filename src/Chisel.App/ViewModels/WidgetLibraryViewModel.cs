using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Chisel.Core.Layout;

namespace Chisel.App.ViewModels;

// One row of the widget library: a widget type, and whether it can be added or where it stands.
// Status is empty for an available widget with no copy placed; otherwise a short note such as "On dashboard" or "2 on dashboard".
public sealed record WidgetLibraryRow(WidgetDefinition Widget, bool CanAdd, string Status)
{
    public string Id => Widget.Id;
    public string Title => Widget.Title;
    public string Icon => Widget.Icon;
    public string Description => Widget.Description;
    public bool HasStatus => Status.Length > 0;
}

// The edit-mode widget library: every registered widget, split into Available (can be added) and On dashboard,
// sorted by name and filtered by a search over name and description. It only reads the layout and adds through
// LayoutViewModel.AddWidgetFromTray, so it takes no part in widget dragging. It closes when edit mode ends.
public partial class WidgetLibraryViewModel : ViewModelBase
{
    private readonly LayoutViewModel _layout;

    public WidgetLibraryViewModel(LayoutViewModel layout)
    {
        _layout = layout;
        _layout.PropertyChanged += OnLayoutChanged;
        Refresh();
    }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    public IReadOnlyList<WidgetLibraryRow> Available { get; private set; } = [];
    public IReadOnlyList<WidgetLibraryRow> OnDashboard { get; private set; } = [];
    public bool HasAvailable => Available.Count > 0;
    public bool HasOnDashboard => OnDashboard.Count > 0;
    public bool HasResults => HasAvailable || HasOnDashboard;
    public bool IsEmpty => !HasResults;

    [RelayCommand]
    private void Toggle()
    {
        if (_layout.IsEditing)
        {
            IsOpen = !IsOpen;
        }
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    // Adds through the existing placement flow. The library stays open and the rows update at once.
    [RelayCommand]
    private void Add(string? widgetId)
    {
        if (widgetId is not null && Available.Any(r => r.Id == widgetId && r.CanAdd))
        {
            _layout.AddWidgetFromTray(widgetId);
        }
    }

    partial void OnQueryChanged(string value) => Refresh();

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutViewModel.Layout))
        {
            Refresh();
        }
        else if (e.PropertyName == nameof(LayoutViewModel.IsEditing) && !_layout.IsEditing)
        {
            IsOpen = false;
            Query = "";
        }
    }

    private void Refresh()
    {
        var placed = _layout.Layout.WidgetIds;
        var query = Query.Trim();
        var available = new List<WidgetLibraryRow>();
        var onDashboard = new List<WidgetLibraryRow>();
        foreach (var widget in WidgetRegistry.AllTypes.OrderBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            if (query.Length > 0
                && !widget.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                && !widget.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }
            var count = placed.Count(id => InstanceIds.TypeOf(id) == widget.Id);
            if (widget.AllowMultiple)
            {
                available.Add(new WidgetLibraryRow(widget, true, count > 0 ? $"{count} on dashboard" : ""));
            }
            else if (count > 0)
            {
                onDashboard.Add(new WidgetLibraryRow(widget, false, "On dashboard"));
            }
            else
            {
                // Hidden (or not placed): still a real widget, so it can be added back.
                available.Add(new WidgetLibraryRow(widget, true, ""));
            }
        }
        Available = available;
        OnDashboard = onDashboard;
        OnPropertyChanged(nameof(Available));
        OnPropertyChanged(nameof(OnDashboard));
        OnPropertyChanged(nameof(HasAvailable));
        OnPropertyChanged(nameof(HasOnDashboard));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
