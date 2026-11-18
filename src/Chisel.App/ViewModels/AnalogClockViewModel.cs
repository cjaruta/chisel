using System;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// The analog clock widget's hands, in degrees clockwise from 12. They follow the header clock, which the
// window's existing 1-second refresh updates, so there is no timer of its own.
public partial class AnalogClockViewModel : ViewModelBase
{
    private readonly ClockViewModel _clock;

    [ObservableProperty]
    public partial double HourAngle { get; private set; }

    [ObservableProperty]
    public partial double MinuteAngle { get; private set; }

    [ObservableProperty]
    public partial double SecondAngle { get; private set; }

    public AnalogClockViewModel(ClockViewModel clock)
    {
        _clock = clock;
        _clock.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClockViewModel.Now))
            {
                Update();
            }
        };
        Update();
    }

    private void Update()
    {
        var angles = ClockMath.ClockAngles(TimeOnly.FromDateTime(_clock.Now.DateTime));
        HourAngle = angles.Hour;
        MinuteAngle = angles.Minute;
        SecondAngle = angles.Second;
    }
}
