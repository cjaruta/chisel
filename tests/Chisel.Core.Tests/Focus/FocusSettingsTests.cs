using Chisel.Core.Focus;

namespace Chisel.Core.Tests.Focus;

public class FocusSettingsTests
{
    [Fact]
    public void Defaults_Are25Focus5Short15LongAfter4Sessions_WithAutoStartOff()
    {
        var s = FocusSettings.Default;

        Assert.Equal((25, 5, 15, 4, false), (s.FocusMinutes, s.ShortBreakMinutes, s.LongBreakMinutes, s.SessionsBeforeLongBreak, s.AutoStartNext));
        Assert.Equal(TimeSpan.FromMinutes(25), s.Duration(FocusPhase.Focus));
        Assert.Equal(TimeSpan.FromMinutes(5), s.Duration(FocusPhase.ShortBreak));
        Assert.Equal(TimeSpan.FromMinutes(15), s.Duration(FocusPhase.LongBreak));
        Assert.Null(FocusValidation.Error(s));
    }

    [Theory]
    [InlineData(0, 5, 15, 4, "Focus must be 1 to 180 minutes.")]
    [InlineData(181, 5, 15, 4, "Focus must be 1 to 180 minutes.")]
    [InlineData(25, 0, 15, 4, "A short break must be 1 to 60 minutes.")]
    [InlineData(25, 5, 121, 4, "A long break must be 1 to 120 minutes.")]
    [InlineData(25, 5, 15, 0, "Sessions before a long break must be 1 to 12.")]
    [InlineData(25, 5, 15, 13, "Sessions before a long break must be 1 to 12.")]
    public void Validation_RejectsValuesOutsideTheLimits(int focus, int shortBreak, int longBreak, int sessions, string error)
    {
        var s = FocusSettings.Default with { FocusMinutes = focus, ShortBreakMinutes = shortBreak, LongBreakMinutes = longBreak, SessionsBeforeLongBreak = sessions };

        Assert.Equal(error, FocusValidation.Error(s));
    }

    [Fact]
    public void Clamped_BringsEveryValueIntoItsLimits()
    {
        var s = new FocusSettings(0, 999, -4, 50, true, 140, FocusFace.Digital, null).Clamped();

        Assert.Equal((1, 60, 1, 12, 100), (s.FocusMinutes, s.ShortBreakMinutes, s.LongBreakMinutes, s.SessionsBeforeLongBreak, s.ChimeVolume));
        Assert.Null(FocusValidation.Error(s));
    }

    [Fact]
    public void Presets_Are25_5_50_10_And15_3()
    {
        Assert.Equal(new[] { "25 / 5", "50 / 10", "15 / 3" }, FocusPresets.All.Select(p => p.Name));
        var applied = FocusPresets.All[1].ApplyTo(FocusSettings.Default with { ChimeVolume = 10 });

        Assert.Equal((50, 10, 30, 10), (applied.FocusMinutes, applied.ShortBreakMinutes, applied.LongBreakMinutes, applied.ChimeVolume));
        Assert.All(FocusPresets.All, p => Assert.Null(FocusValidation.Error(p.ApplyTo(FocusSettings.Default))));
    }
}
