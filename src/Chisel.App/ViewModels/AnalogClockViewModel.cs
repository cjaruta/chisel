using System;
using Chisel.Core.Clock;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chisel.App.ViewModels;

// Fires once per second while running. A seam so tests can drive the clock without waiting.
public interface ITickTimer
{
    event Action? Tick;
    void Start();
    void Stop();
}

// The analog clock widget's hands, in degrees clockwise from 12, following local time while attached.
public partial class AnalogClockViewModel : ViewModelBase
{
    private readonly TimeProvider _time;
    private readonly ITickTimer _timer;
    private bool _attached;

    [ObservableProperty]
    public partial double HourAngle { get; private set; }

    [ObservableProperty]
    public partial double MinuteAngle { get; private set; }

    [ObservableProperty]
    public partial double SecondAngle { get; private set; }

    public AnalogClockViewModel(TimeProvider time, ITickTimer timer)
    {
        _time = time;
        _timer = timer;
        Update();
    }

    // Shows the current time and starts ticking. Safe to call twice.
    public void Attach()
    {
        if (_attached)
        {
            return;
        }
        _attached = true;
        Update();
        _timer.Tick += Update;
        _timer.Start();
    }

    // Stops ticking and lets go of the timer.
    public void Detach()
    {
        if (!_attached)
        {
            return;
        }
        _attached = false;
        _timer.Stop();
        _timer.Tick -= Update;
    }

    private void Update()
    {
        var angles = ClockMath.ClockAngles(TimeOnly.FromDateTime(_time.GetLocalNow().DateTime));
        HourAngle = angles.Hour;
        MinuteAngle = angles.Minute;
        SecondAngle = angles.Second;
    }
}
