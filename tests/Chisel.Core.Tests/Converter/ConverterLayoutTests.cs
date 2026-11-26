using Chisel.Core.Converter;

namespace Chisel.Core.Tests.Converter;

public class ConverterLayoutTests
{
    [Theory]
    [InlineData(200, 90, ConverterDetail.Compact)]
    [InlineData(400, 120, ConverterDetail.Compact)]
    [InlineData(ConverterLayout.StandardMinWidth + 20, ConverterLayout.StandardMinHeight + 20, ConverterDetail.Standard)]
    [InlineData(ConverterLayout.FullMinWidth + 20, ConverterLayout.StandardMinHeight + 20, ConverterDetail.Standard)]
    [InlineData(ConverterLayout.FullMinWidth + 20, ConverterLayout.FullMinHeight + 20, ConverterDetail.Full)]
    public void PicksDetail_FromNamedThresholds(double width, double height, ConverterDetail expected) =>
        Assert.Equal(expected, ConverterLayout.For(width, height).Detail);

    [Fact]
    public void OnlyLargerSizes_ShowTabsSwapAndCommonList()
    {
        var compact = ConverterLayout.For(220, 90);
        Assert.False(compact.ShowCategoryPicker || compact.ShowButtons || compact.ShowTabs);
        Assert.Equal(0, compact.CommonRows);

        var standard = ConverterLayout.For(300, 160);
        Assert.True(standard.ShowCategoryPicker && standard.ShowButtons);
        Assert.False(standard.ShowTabs);

        var full = ConverterLayout.For(420, 400);
        Assert.True(full.ShowTabs && full.ShowButtons);
        Assert.False(full.ShowCategoryPicker);
        Assert.InRange(full.CommonRows, ConverterLayout.MinCommonRows, ConverterLayout.MaxCommonRows);
    }

    [Fact]
    public void ScalesWithWidth_WithinLimits()
    {
        var narrow = ConverterLayout.For(260, 500);
        var wide = ConverterLayout.For(420, 500);
        Assert.True(wide.FieldHeight > narrow.FieldHeight);
        Assert.Equal(ConverterLayout.BaseField * ConverterLayout.MaxScale, ConverterLayout.For(3000, 3000).FieldHeight, 6);
    }

    [Fact]
    public void CommonRows_GrowWithHeight_UpToTheCap()
    {
        Assert.True(ConverterLayout.For(420, 420).CommonRows > ConverterLayout.For(420, 300).CommonRows);
        Assert.Equal(ConverterLayout.MaxCommonRows, ConverterLayout.For(420, 2000).CommonRows);
    }

    [Fact]
    public void NeverTallerOrWiderThanItsRoom()
    {
        for (var w = 180; w <= 900; w += 20)
        {
            for (var h = 60; h <= 900; h += 10)
            {
                var l = ConverterLayout.For(w, h);
                Assert.True(l.ContentHeight <= h - 2 * l.Pad + 0.001, $"{w}x{h}: {l.ContentHeight}");
                Assert.True(l.RowWidth <= w - 2 * l.Pad + 0.001, $"{w}x{h}: width");
                Assert.True(l.ValueWidth >= ConverterLayout.MinValueWidth * l.Scale - 0.001, $"{w}x{h}: value box");
            }
        }
    }
}

public class ConverterSettingsTests
{
    [Fact]
    public void Valid_PutsUnknownIdsBackToDefaults()
    {
        Assert.Equal(ConverterSettings.Default, new ConverterSettings("nope", "m", "ft").Valid());
        Assert.Equal(new ConverterSettings("mass", "kg", "lb"), new ConverterSettings("mass", "parsec", "lb").Valid());
        Assert.Equal(new ConverterSettings("data", "GiB", "GB"), new ConverterSettings("data", "GiB", "GB").Valid());
    }
}
