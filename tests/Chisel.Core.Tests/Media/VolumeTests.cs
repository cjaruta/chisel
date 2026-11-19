using Chisel.Core.Media;

namespace Chisel.Core.Tests.Media;

public class VolumeTests
{
    [Theory]
    [InlineData(-0.5, 0)]
    [InlineData(0.4, 0.4)]
    [InlineData(1.7, 1)]
    [InlineData(double.NaN, 0)]
    public void Clamp_KeepsTheLevelBetweenZeroAndOne(double level, double expected) =>
        Assert.Equal(expected, VolumeMath.Clamp(level));

    [Theory]
    [InlineData(0.8, true, VolumeIcon.Muted)]
    [InlineData(0.0, false, VolumeIcon.Muted)]
    [InlineData(0.2, false, VolumeIcon.Low)]
    [InlineData(0.5, false, VolumeIcon.Medium)]
    [InlineData(0.9, false, VolumeIcon.High)]
    public void Icon_FollowsMuteAndLevel(double level, bool muted, VolumeIcon expected) =>
        Assert.Equal(expected, VolumeMath.Icon(level, muted));

    [Theory]
    [InlineData(0.256, "26%")]
    [InlineData(1.0, "100%")]
    public void Percent_IsWholeNumbers(double level, string expected) =>
        Assert.Equal(expected, VolumeMath.Percent(level));

    [Theory]
    [InlineData(109, true)]
    [InlineData(110, false)]
    public void Layout_PutsVolumeBesideTheButtonsWhenShort(double height, bool beside) =>
        Assert.Equal(beside, MediaLayout.For(400, height).VolumeBesideButtons);
}
