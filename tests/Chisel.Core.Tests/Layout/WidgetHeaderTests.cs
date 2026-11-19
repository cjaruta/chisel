using Chisel.Core.Layout;

namespace Chisel.Core.Tests.Layout;

public class WidgetHeaderTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EffectiveShowHeader_WithNoChoice_UsesTheWidgetDefault(bool widgetDefault)
    {
        Assert.Equal(widgetDefault, WidgetHeaders.EffectiveShowHeader(new WidgetPlacement("tasks"), widgetDefault));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void EffectiveShowHeader_WithAChoice_WinsOverTheDefault(bool widgetDefault, bool choice)
    {
        var placement = new WidgetPlacement("tasks", 1, choice);

        Assert.Equal(choice, WidgetHeaders.EffectiveShowHeader(placement, widgetDefault));
    }

    [Fact]
    public void SetShowHeader_ChangesOnlyThatWidget_AndSurvivesMovesAndResizes()
    {
        var layout = new WidgetLayout(new[]
        {
            new LayoutColumn(1, new[] { new WidgetPlacement("a"), new WidgetPlacement("b") }),
            new LayoutColumn(1, new[] { new WidgetPlacement("c") }),
        });

        var changed = layout.SetShowHeader("b", true).Move("b", 1, 0).Resize("b", 0.1, 0.1);

        Assert.True(changed.Columns[1].Widgets.Single(w => w.WidgetId == "b").ShowHeader);
        Assert.All(changed.Columns.SelectMany(c => c.Widgets).Where(w => w.WidgetId != "b"), w => Assert.Null(w.ShowHeader));
    }
}
