using Chisel.Core.Media;

namespace Chisel.Core.Tests.Media;

public class MediaCoreTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(65, "1:05")]
    [InlineData(3599, "59:59")]
    [InlineData(3725, "1:02:05")]
    public void FormatTime_UsesMinutesAndSeconds_AndHoursWhenNeeded(int seconds, string expected) =>
        Assert.Equal(expected, MediaFormat.FormatTime(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void FormatTime_ClampsNegativeToZero() =>
        Assert.Equal("0:00", MediaFormat.FormatTime(TimeSpan.FromSeconds(-4)));

    [Theory]
    [InlineData(30, 120, 0.25)]
    [InlineData(200, 120, 1.0)]
    [InlineData(10, 0, 0.0)]
    public void Progress_IsAFractionClampedToZeroAndOne(int position, int duration, double expected) =>
        Assert.Equal(expected, MediaFormat.Progress(TimeSpan.FromSeconds(position), TimeSpan.FromSeconds(duration)), 3);

    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("chrome.exe", "Chrome")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "ZuneMusic")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SourceName_ReadsAFriendlyNameFromTheAppId(string? id, string expected) =>
        Assert.Equal(expected, MediaFormat.SourceName(id));

    [Fact]
    public void Controls_ReportWhatIsAvailable()
    {
        var info = new MediaSessionInfo { Controls = MediaControls.Pause | MediaControls.Next };
        Assert.True(info.Controls.HasFlag(MediaControls.Next));
        Assert.False(info.Controls.HasFlag(MediaControls.Previous));
    }

    [Theory]
    [InlineData(300, 86, 86, false, false)]
    [InlineData(300, 150, 96, true, true)]
    [InlineData(150, 86, 0, false, false)]
    // Short and narrow: the buttons and the volume share a row, so the cover gives way rather than push them off the card.
    [InlineData(215, 96, 0, false, false)]
    public void Layout_ScalesWithTheCard(double width, double height, double art, bool album, bool source)
    {
        var layout = MediaLayout.For(width, height);
        Assert.Equal(art, layout.ArtSize);
        Assert.Equal(album, layout.ShowAlbum);
        Assert.Equal(source, layout.ShowSource);
    }
}
