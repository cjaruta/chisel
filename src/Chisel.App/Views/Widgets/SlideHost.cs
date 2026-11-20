using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Chisel.App.Views.Widgets;

// Shows one child at a time and slides between them vertically: the new one comes in from below when moving forward (the wheel
// turned down) and from above when moving back, while the old one leaves the other way. The motion is a short timer that only runs
// during a change; with a zero SlideDuration, or before the first layout, the change is immediate.
public sealed class SlideHost : Grid
{
    // The length of one slide. Tests set this to zero so a change is complete as soon as it is made.
    public static TimeSpan SlideDuration { get; set; } = TimeSpan.FromMilliseconds(160);

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private Control? _outgoing;
    private Control? _incoming;
    private int _step;
    private long _startedAt;

    public SlideHost()
    {
        ClipToBounds = true;
        _timer.Tick += (_, _) => Advance();
    }

    public Control? Current { get; private set; }

    // Brings `next` in. step is +1 forward, -1 back; 0 (the first show) never animates.
    public void Show(Control? next, int step)
    {
        if (ReferenceEquals(next, Current))
        {
            return;
        }
        Finish();
        var previous = Current;
        Current = next;
        if (next is not null)
        {
            Children.Remove(next);
            Children.Add(next);
        }
        var height = Bounds.Height;
        if (step == 0 || SlideDuration <= TimeSpan.Zero || height <= 0 || previous is null || next is null || !IsEffectivelyVisible)
        {
            if (previous is not null)
            {
                Children.Remove(previous);
                Reset(previous);
            }
            if (next is not null)
            {
                Reset(next);
            }
            return;
        }
        _outgoing = previous;
        _incoming = next;
        _step = step;
        _startedAt = Environment.TickCount64;
        Move(0);
        _timer.Start();
    }

    private void Advance()
    {
        var t = (Environment.TickCount64 - _startedAt) / SlideDuration.TotalMilliseconds;
        if (t >= 1)
        {
            Finish();
            return;
        }
        Move(1 - Math.Pow(1 - t, 3)); // ease out
    }

    private void Move(double progress)
    {
        var height = Bounds.Height * _step;
        Offset(_incoming, height * (1 - progress));
        Offset(_outgoing, -height * progress);
    }

    // Ends a slide in progress: the old child goes, the new one sits at rest.
    private void Finish()
    {
        _timer.Stop();
        if (_outgoing is not null)
        {
            Children.Remove(_outgoing);
            Reset(_outgoing);
        }
        if (_incoming is not null)
        {
            Reset(_incoming);
        }
        _outgoing = null;
        _incoming = null;
    }

    private static void Offset(Control? child, double y)
    {
        if (child is null)
        {
            return;
        }
        if (child.RenderTransform is not TranslateTransform move)
        {
            child.RenderTransform = move = new TranslateTransform();
        }
        move.Y = y;
    }

    private static void Reset(Control child) => Offset(child, 0);
}
