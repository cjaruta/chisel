using Chisel.Core.Clock;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Tests.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Repositories;

public class ClockStyleRepositoryTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Empty_ReturnsTheDefaultStyle()
    {
        using var context = _fixture.CreateContext();
        Assert.Equal(ClockStyle.Default, new ClockStyleRepository(context).Get());
    }

    [Fact]
    public void Save_RoundTripsEveryOption_AndReplacesThePreviousStyle()
    {
        var style = new ClockStyle
        {
            Alignment = ClockAlignment.Right, Shape = DialShape.RoundedSquare, BorderWidth = 5, Markers = MarkerStyle.Dots,
            EmphasizeQuarters = false, Numerals = NumeralStyle.Roman, Hands = HandStyle.Leaf, HourThickness = 6, MinuteThickness = 5,
            SecondThickness = 2, ShowSeconds = false, Colors = new ClockColors("#112233", null, "#445566", null, "#778899", "#AABBCC"),
        };
        using (var context = _fixture.CreateContext())
        {
            var repository = new ClockStyleRepository(context);
            repository.Save(ClockStyle.Default);
            repository.Save(style);
        }

        using var again = _fixture.CreateContext();
        Assert.Equal(style, new ClockStyleRepository(again).Get());
        Assert.Single(again.ClockStyles);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void TheDialOptions_RoundTrip_AndAnOldRowReadsAsAPlainDial()
    {
        var style = ClockPresets.Apply("chrono", ClockStyle.Default) with { Stopwatch = StopwatchDisplay.Analog };
        using (var context = _fixture.CreateContext())
        {
            new ClockStyleRepository(context).Save(style);
        }

        using var read = _fixture.CreateContext();
        var back = new ClockStyleRepository(read).Get();
        Assert.Equal(style, back);
        Assert.Equal(DialKind.Chronograph, back.Kind);
        Assert.Equal("#101216", back.DialFill);
        Assert.True(back.Tachymeter && back.MinuteTrack);
        Assert.Equal(StopwatchDisplay.Analog, back.Stopwatch);
    }
}
