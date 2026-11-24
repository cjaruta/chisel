using Chisel.Core.Theme;

namespace Chisel.Core.Tests.Theme;

// The light mode is a soft warm paper, not white, and everything on it reads.
public class LightModeTests
{
    private static readonly ThemeDefinition Light = ThemeModes.ById("light");

    [Fact]
    public void NoLightSurfaceIsWhite_SoItIsNotGlaring()
    {
        foreach (var surface in new[] { Light.Window, Light.Card, Light.Raised })
        {
            Assert.True(surface.Luminance <= 0.9, $"{surface.ToHex()} is too bright");
        }
        Assert.True(Light.Card.Luminance > Light.Window.Luminance, "cards sit above the window");
        Assert.True(Light.Window.Luminance > Light.Raised.Luminance, "raised controls are a step in");
        Assert.NotEqual(AccentDerivation.Black, Light.Text); // the text is soft, not pure black
    }

    [Fact]
    public void TheTextTiersAndTheControlOutlines_ReadOnEverySurface()
    {
        foreach (var surface in new[] { Light.Window, Light.Card, Light.Raised })
        {
            Assert.True(Rgb.Contrast(Light.Text, surface) >= 10, $"text {surface.ToHex()}");
            Assert.True(Rgb.Contrast(Light.TextSecondary, surface) >= 5.5, $"secondary {surface.ToHex()}");
            Assert.True(Rgb.Contrast(Light.TextDisabled, surface) >= 3, $"disabled {surface.ToHex()}");
            Assert.True(Rgb.Contrast(Light.ControlBorder, surface) >= 3, $"control border {surface.ToHex()}");
        }
    }

    [Theory]
    [InlineData("#7D93C4")] [InlineData("#8DB38A")] [InlineData("#62AEB0")] [InlineData("#D0A660")] [InlineData("#CC7F66")]
    [InlineData("#CC8BA0")] [InlineData("#A693CF")] [InlineData("#9C978F")] [InlineData("#4C8EF2")] [InlineData("#5FC46A")]
    [InlineData("#2EC4C4")] [InlineData("#F2B53A")] [InlineData("#F07148")] [InlineData("#EE6FA0")] [InlineData("#AE73F2")]
    [InlineData("#B3AA9C")] [InlineData("#FFFFFF")]
    public void ABlockOnTheLightMode_ReadsWhateverItsColor(string stripe)
    {
        var look = LightBlockLook.For(Rgb.Parse(stripe), Light);

        Assert.True(Rgb.Contrast(look.Text, look.Fill) >= 7, $"title on {stripe}");
        Assert.True(Rgb.Contrast(look.TimeText, look.Fill) >= 4.5, $"time on {stripe}");
        Assert.True(Rgb.Contrast(look.Stripe, Light.Card) >= 3, $"stripe of {stripe}");
        Assert.True(look.Fill.Luminance > Light.Raised.Luminance - 0.2, "the fill is a soft tint, not a dark slab");
    }
}
