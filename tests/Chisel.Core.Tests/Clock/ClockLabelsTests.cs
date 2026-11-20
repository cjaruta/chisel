using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

public class ClockLabelsTests
{
    [Theory]
    [InlineData(NumeralStyle.None, "")]
    [InlineData(NumeralStyle.Twelve, "12:12")]
    [InlineData(NumeralStyle.Four, "12:12,3:3,6:6,9:9")]
    [InlineData(NumeralStyle.All, "1:1,2:2,3:3,4:4,5:5,6:6,7:7,8:8,9:9,10:10,11:11,12:12")]
    [InlineData(NumeralStyle.Roman, "1:I,2:II,3:III,4:IV,5:V,6:VI,7:VII,8:VIII,9:IX,10:X,11:XI,12:XII")]
    public void For_ListsTheHoursAndTheirText(NumeralStyle style, string expected)
    {
        var labels = string.Join(",", ClockLabels.For(style).Select(l => $"{l.Hour}:{l.Text}"));
        Assert.Equal(expected, labels);
    }

    [Theory]
    [InlineData(12, 0)]
    [InlineData(3, 90)]
    [InlineData(9, 270)]
    public void AngleOf_IsDegreesClockwiseFromTwelve(int hour, double degrees) => Assert.Equal(degrees, ClockLabels.AngleOf(hour));
}
