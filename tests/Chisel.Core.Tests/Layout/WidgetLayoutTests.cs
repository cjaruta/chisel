using Chisel.Core.Layout;
using Xunit;

namespace Chisel.Core.Tests.Layout;

public class WidgetLayoutTests
{
    [Fact]
    public void Move_WidgetToSideColumn_AppearsInSide()
    {
        // Arrange
        var layout = new WidgetLayout();

        // Act
        layout.Move("clock", Column.Side, 0);

        // Assert
        Assert.Contains("clock", layout.Side);
    }

    [Fact]
    public void Move_WidgetAlreadyInMain_RemovesItFromMainFirst()
    {
        // Arrange
        var layout = new WidgetLayout();
        layout.Move("clock", Column.Main, 0);

        // Act
        layout.Move("clock", Column.Side, 0);

        // Assert
        Assert.DoesNotContain("clock", layout.Main);
        Assert.Contains("clock", layout.Side);
    }

    [Fact]
    public void Remove_PlacedWidget_TakesItOutOfBothColumns()
    {
        // Arrange
        var layout = new WidgetLayout();
        layout.Move("clock", Column.Side, 0);

        // Act
        layout.Remove("clock");

        // Assert
        Assert.DoesNotContain("clock", layout.Side);
        Assert.DoesNotContain("clock", layout.Main);
    }

    [Fact]
    public void Reset_AfterChanges_RestoresGivenDefault()
    {
        // Arrange
        var layout = new WidgetLayout();
        layout.Move("clock", Column.Side, 0);
        var defaultSide = new[] { "clock", "now" };
        var defaultMain = new[] { "plan" };

        // Act
        layout.Reset(defaultSide, defaultMain);

        // Assert
        Assert.Equal(defaultSide, layout.Side);
        Assert.Equal(defaultMain, layout.Main);
    }
}