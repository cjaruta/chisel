using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

public partial class EditableBlockRow : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Start { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string End { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Error { get; set; }
}
