using System;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// The analog clock widget's hands, in degrees clockwise from 12. They follow the header clock, which the
// window's existing 1-second refresh updates, so there is no timer of its own.
public partial class AnalogClockViewModel : ViewModelBase
{
    private readonly ClockViewModel _clock;
    private readonly TimeDisplay _display;

    [ObservableProperty]
    public partial double HourAngle { get; private set; }

    [ObservableProperty]
    public partial double MinuteAngle { get; private set; }

    [ObservableProperty]
    public partial double SecondAngle { get; private set; }

    // The 24-hour hand (once round a day): the hour hand of a 24-hour dial and the day sub-dial of a chronograph.
    [ObservableProperty]
    public partial double Hour24Angle { get; private set; }

    // What a screen reader says for the face: the time, to the minute (so it changes once a minute, not every second).
    [ObservableProperty]
    public partial string Description { get; private set; } = "Clock";

    public AnalogClockViewModel(ClockViewModel clock, TimeDisplay? display = null)
    {
        _clock = clock;
        _display = display ?? new TimeDisplay();
        _display.PropertyChanged += (_, _) => Update();
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
        var time = TimeOnly.FromDateTime(_clock.Now.DateTime);
        Hour24Angle = ClockMath.Hour24Angle(time);
        var spoken = $"Clock, {_display.Time(new TimeOnly(time.Hour, time.Minute), withSeconds: false)}";
        if (spoken != Description)
        {
            Description = spoken;
        }
    }
}
