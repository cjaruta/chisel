using Chisel.Core.Calculator;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// The Calculator page in the Widgets settings: whether the advanced panel is on offer, and the angle mode the calculator starts in.
// Each change is saved at once.
public partial class CalculatorSettingsViewModel : ViewModelBase
{
    private readonly ICalculatorSettingsRepository? _repository;
    private readonly bool _loading = true;

    [ObservableProperty]
    public partial bool Advanced { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDegrees))]
    [NotifyPropertyChangedFor(nameof(IsRadians))]
    public partial AngleMode Angle { get; set; }

    public bool IsDegrees
    {
        get => Angle == AngleMode.Degrees;
        set
        {
            if (value)
            {
                Angle = AngleMode.Degrees;
            }
        }
    }

    public bool IsRadians
    {
        get => Angle == AngleMode.Radians;
        set
        {
            if (value)
            {
                Angle = AngleMode.Radians;
            }
        }
    }

    public CalculatorSettingsViewModel(ICalculatorSettingsRepository? repository)
    {
        _repository = repository;
        var saved = repository?.Get() ?? CalculatorSettings.Default;
        Advanced = saved.Advanced;
        Angle = saved.Angle;
        _loading = false;
    }

    partial void OnAdvancedChanged(bool value) => Save();

    partial void OnAngleChanged(AngleMode value) => Save();

    private void Save()
    {
        if (!_loading)
        {
            _repository?.Save(new CalculatorSettings(Advanced, Angle));
        }
    }
}
