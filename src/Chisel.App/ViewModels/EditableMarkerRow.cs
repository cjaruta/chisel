using Chisel.Core.Schedule;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

public partial class EditableMarkerRow : ViewModelBase
{
    [ObservableProperty]
    public partial string Label { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Time { get; set; } = string.Empty;

    [ObservableProperty]
    public partial MarkerKind Kind { get; set; } = MarkerKind.Reminder;

    [ObservableProperty]
    public partial string? Error { get; set; }
}
