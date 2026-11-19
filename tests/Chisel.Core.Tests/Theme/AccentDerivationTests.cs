using Chisel.Core.Theme;

namespace Chisel.Core.Tests.Theme;

public class AccentDerivationTests
{
    private static readonly Rgb Window = Rgb.Parse("#14161A");
    private static readonly Rgb Card = Rgb.Parse("#1F222A");
    private static readonly Rgb LightWindow = Rgb.Parse("#F3F4F6");
    private static readonly Rgb LightCard = Rgb.Parse("#FFFFFF");

    [Theory]
    [InlineData("#F5A524", 245, 165, 36)]
    [InlineData("f5a524", 245, 165, 36)]
    [InlineData("#000", 0, 0, 0)]
    public void Parse_ReadsHex(string hex, int r, int g, int b) => Assert.Equal(new Rgb((byte)r, (byte)g, (byte)b), Rgb.Parse(hex));

    [Theory]
    [InlineData("")]
    [InlineData("orange")]
    [InlineData("#12345")]
    [InlineData(null)]
    public void TryParse_RejectsBadInput(string? hex) => Assert.False(Rgb.TryParse(hex, out _));

    [Fact]
    public void Contrast_BlackOnWhiteIs21() => Assert.Equal(21, Rgb.Contrast(Rgb.Parse("#000000"), Rgb.Parse("#FFFFFF")), 1);

    [Fact]
    public void OnAccent_IsBlackOnBrightAndWhiteOnDark()
    {
        Assert.Equal(Rgb.Parse("#000000"), AccentDerivation.Derive(Rgb.Parse("#F5A524"), Window, Card).OnAccent);
        Assert.Equal(Rgb.Parse("#FFFFFF"), AccentDerivation.Derive(Rgb.Parse("#1D4ED8"), LightWindow, LightCard).OnAccent);
    }

    [Fact]
    public void GoodAccent_IsKept()
    {
        var accent = Rgb.Parse("#F5A524");
        Assert.Equal(accent, AccentDerivation.Derive(accent, Window, Card).Fill);
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#0A1A5C")]
    [InlineData("#FFE600")]
    [InlineData("#F5A524")]
    public void Tokens_MeetContrastOnDarkAndLightSurfaces(string hex)
    {
        foreach (var (window, card) in new[] { (Window, Card), (LightWindow, LightCard) })
        {
            var t = AccentDerivation.Derive(Rgb.Parse(hex), window, card);
            Assert.True(Rgb.Contrast(t.Text, card) >= 4.5, $"text on card {hex}");
            Assert.True(Rgb.Contrast(t.Text, window) >= 4.5, $"text on window {hex}");
            foreach (var fill in new[] { t.Fill, t.Hover, t.Pressed })
            {
                Assert.True(Rgb.Contrast(fill, card) >= 3, $"fill {fill.ToHex()} on card {hex}");
                Assert.True(Rgb.Contrast(fill, window) >= 3, $"fill {fill.ToHex()} on window {hex}");
                Assert.True(Rgb.Contrast(t.OnAccent, fill) >= 4.5, $"on-accent {fill.ToHex()} {hex}");
            }
        }
    }

    [Fact]
    public void HoverAndPressed_DifferFromFillAndEachOther()
    {
        var t = AccentDerivation.Derive(Rgb.Parse("#F5A524"), Window, Card);
        Assert.NotEqual(t.Fill, t.Hover);
        Assert.NotEqual(t.Hover, t.Pressed);
    }

    [Fact]
    public void Subtle_IsAFaintTintOfTheCard()
    {
        var t = AccentDerivation.Derive(Rgb.Parse("#F5A524"), Window, Card);
        Assert.True(Rgb.Contrast(t.Subtle, Card) < 1.5);
        Assert.NotEqual(Card, t.Subtle);
    }
}
