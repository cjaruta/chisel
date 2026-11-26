using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

// The timer keeps its time and gives up buttons in a fixed order as its slot shrinks, never overflowing the room it has.
public class ClockTimerLayoutTests
{
    private static IEnumerable<(double W, double H)> Cells() =>
        from w in new[] { 90.0, 120, 160, 200, 260, 340, 480, 700 }
        from h in new[] { 40.0, 60, 80, 100, 120, 150, 190, 240, 320, 450 }
        select (w, h);

    [Fact]
    public void ARoomySlot_ShowsEverything_AtTheFullSize()
    {
        var s = ClockTypeScale.For(340, 320);
        var fit = ClockTimerLayout.For(s);

        Assert.Equal((s.Display, TimerFit.AllPresets, true, true), (fit.Display, fit.Presets, fit.ShowReset, fit.ShowStart));
    }

    [Fact]
    public void WhenTheSlotShrinks_ThePartsGoInOrder_PresetsThenResetThenStart_AndTheTimeStays()
    {
        // Walk down in height: what is shown only ever gets smaller, and the order of giving up is the documented one.
        var last = (Presets: TimerFit.AllPresets, Reset: true, Start: true);
        for (var h = 450.0; h >= 30; h -= 5)
        {
            var fit = ClockTimerLayout.For(ClockTypeScale.For(260, h));
            Assert.True(fit.Display >= ClockTimerLayout.FloorDisplay);
            Assert.True(fit.Presets <= last.Presets, $"presets come back at {h}");
            Assert.True(!fit.ShowReset || last.Reset, $"reset comes back at {h}");
            Assert.True(!fit.ShowStart || last.Start, $"start comes back at {h}");
            Assert.True(fit.Presets == 0 || fit.ShowReset && fit.ShowStart, $"presets stay while a button goes at {h}");
            Assert.True(!fit.ShowReset || fit.ShowStart, $"reset stays while start goes at {h}");
            last = (fit.Presets, fit.ShowReset, fit.ShowStart);
        }
        Assert.Equal((0, false, false), last);
    }

    [Fact]
    public void WhateverIsShown_FitsTheRoom_WheneverTheTimeCanBeAtItsLegibleMinimum()
    {
        foreach (var (w, h) in Cells())
        {
            var s = ClockTypeScale.For(w, h);
            var fit = ClockTimerLayout.For(s);

            Assert.InRange(fit.Display, ClockTimerLayout.FloorDisplay, s.Display);
            // Only a slot too small even for the time alone may overflow.
            if (h - 2 * s.Pad >= ClockTimerLayout.MinDisplay * 1.3 && w - 2 * s.Pad >= ClockTimerLayout.MinDisplay * 4.1)
            {
                Assert.True(fit.Height <= h - 2 * s.Pad + 0.001, $"{w}x{h}: {fit.Height:0.#} does not fit");
            }
        }
    }

    [Fact]
    public void TheTimeIsNeverMadeNarrowerThanTheSlotCanShow()
    {
        foreach (var (w, h) in Cells())
        {
            var s = ClockTypeScale.For(w, h);
            var fit = ClockTimerLayout.For(s);
            var needed = fit.Display * 7 * 0.58;

            Assert.True(needed <= Math.Max(w - 2 * s.Pad, ClockTimerLayout.FloorDisplay * 7 * 0.58) + 0.001, $"{w}x{h}: the time is too wide");
        }
    }

    [Fact]
    public void TheClockNumerals_ShowFromTheNamedDiameter_AndNotBelow()
    {
        Assert.True(ClockDialLayout.ShowsNumerals(ClockDialLayout.NumeralsFromDiameter));
        Assert.False(ClockDialLayout.ShowsNumerals(ClockDialLayout.NumeralsFromDiameter - 0.1));
        Assert.All(Enumerable.Range(0, 400), d => Assert.Equal(d >= ClockDialLayout.NumeralsFromDiameter, ClockDialLayout.ShowsNumerals(d)));
    }
}
