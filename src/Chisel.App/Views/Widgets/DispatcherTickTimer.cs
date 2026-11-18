using System;
using Avalonia.Threading;
using Chisel.App.ViewModels;

namespace Chisel.App.Views.Widgets;

// Ticks just after each whole second of local time, so a second hand steps once per second without drifting.
public sealed class DispatcherTickTimer : ITickTimer
{
    private readonly TimeProvider _time;
    private readonly DispatcherTimer _timer = new();

    public DispatcherTickTimer(TimeProvider time)
    {
        _time = time;
        _timer.Tick += (_, _) =>
        {
            _timer.Interval = UntilNextSecond();
            Tick?.Invoke();
        };
    }

    public event Action? Tick;

    public void Start()
    {
        _timer.Interval = UntilNextSecond();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private TimeSpan UntilNextSecond() => TimeSpan.FromMilliseconds(1005 - _time.GetUtcNow().Millisecond);
}
