using Chisel.App.Services;

namespace Chisel.App.Tests.Services;

public class SoftChimeTests
{
    private static double Peak(byte[] pcm)
    {
        var peak = 0;
        for (var i = 0; i < pcm.Length; i += 2)
        {
            peak = Math.Max(peak, Math.Abs((int)(short)(pcm[i] | pcm[i + 1] << 8)));
        }
        return peak / (double)short.MaxValue;
    }

    [Fact]
    public void TheChime_IsShortAndQuiet_AndFollowsTheVolume()
    {
        var full = SoftChime.Build(100);
        var half = SoftChime.Build(50);

        Assert.True(full.Length / 2.0 / SoftChime.SampleRate < 1, "longer than a second");
        Assert.InRange(Peak(full), 0.05, SoftChime.PeakAtFullVolume);
        Assert.True(Peak(half) < Peak(full) / 2);
        Assert.Equal(0, Peak(SoftChime.Build(0)));
    }
}
