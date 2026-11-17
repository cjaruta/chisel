using Avalonia;
using Chisel.App.Controls;

namespace Chisel.App.Tests.Controls;

public class DayGridPanelTests
{
    [Fact]
    public void ItemRect_Block_IsPlacedByItsTimesAndSizedByItsDuration()
    {
        var rect = DayGridPanel.ItemRect(startMinute: 480, endMinute: 660, column: 0, columnCount: 1,
            width: 300, desiredHeight: 0, pixelsPerMinute: 1);

        Assert.Equal(new Rect(0, 480, 300, 180), rect);
    }

    [Fact]
    public void ItemRect_Block_ScalesWithPixelsPerMinute()
    {
        var rect = DayGridPanel.ItemRect(60, 90, 0, 1, 300, 0, pixelsPerMinute: 2);

        Assert.Equal(new Rect(0, 120, 300, 60), rect);
    }

    [Fact]
    public void ItemRect_OverlappingBlock_TakesItsShareOfTheWidth()
    {
        var rect = DayGridPanel.ItemRect(480, 540, column: 1, columnCount: 3, width: 300, desiredHeight: 0, pixelsPerMinute: 1);

        Assert.Equal(new Rect(100, 480, 100, 60), rect);
    }

    [Fact]
    public void ItemRect_PointItem_IsCenteredOnItsTimeAndFullWidth()
    {
        var rect = DayGridPanel.ItemRect(600, double.NaN, 0, 1, width: 300, desiredHeight: 20, pixelsPerMinute: 1);

        Assert.Equal(new Rect(0, 590, 300, 20), rect);
    }

    [Theory]
    [InlineData(0, 0)]        // midnight at the top: kept inside instead of half above
    [InlineData(1440, 1420)]  // midnight at the bottom: kept inside instead of half below
    [InlineData(5, 0)]
    public void ItemRect_PointItemNearTheEdges_StaysInsideTheDay(double startMinute, double expectedTop)
    {
        var rect = DayGridPanel.ItemRect(startMinute, double.NaN, 0, 1, width: 300, desiredHeight: 20, pixelsPerMinute: 1);

        Assert.Equal(expectedTop, rect.Y);
    }

    [Theory]
    [InlineData(1.0, 1440)]
    [InlineData(1.0667, 1536.048)]
    public void DayHeight_IsTheWholeDayAtTheGivenScale(double pixelsPerMinute, double expected)
    {
        Assert.Equal(expected, DayGridPanel.DayHeight(pixelsPerMinute), 3);
    }
}
