using Chisel.Core.Clock;
using Chisel.Core.Theme;

namespace Chisel.Core.Tests.Clock;

// The extra analog dials: classic white with Roman numerals, a slim modern dial, a dark chronograph with sub-dials and a tachymeter
// ring, a 24-hour dial, a railway dial and a pilot dial with luminous markers. They are data (ClockPresets), drawn by the one renderer.
public class ClockDialsTests
{
    private static ClockPreset Preset(string id) => ClockPresets.All.Single(p => p.Id == id);

    [Fact]
    public void TheNewFaces_AreBuiltIn_WithUniqueIdsAndTitles()
    {
        var ids = ClockPresets.All.Select(p => p.Id).ToList();
        var titles = ClockPresets.All.Select(p => p.Title).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(titles.Count, titles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var id in new[] { "classic", "slim", "chrono", "hours24", "railway", "pilot" })
        {
            Assert.Contains(id, ids);
        }
        Assert.Equal(12, ids.Count); // the six that were there, then the six new ones
    }

    [Fact]
    public void EachNewFace_HasItsOwnLook()
    {
        Assert.Equal(NumeralStyle.Roman, Preset("classic").Style.Numerals);
        Assert.NotNull(Preset("classic").Style.DialFill);
        Assert.Equal(NumeralStyle.None, Preset("slim").Style.Numerals);
        Assert.True(Preset("slim").Style.HourThickness <= 2.5);
        Assert.Equal(DialKind.Chronograph, Preset("chrono").Style.Kind);
        Assert.True(Preset("chrono").Style.Tachymeter);
        Assert.Equal(DialKind.TwentyFourHour, Preset("hours24").Style.Kind);
        Assert.True(Preset("railway").Style.MinuteTrack);
        Assert.Equal(NumeralStyle.All, Preset("railway").Style.Numerals);
        Assert.Equal(MarkerStyle.Lume, Preset("pilot").Style.Markers);
    }

    [Theory]
    [InlineData("classic")]
    [InlineData("chrono")]
    [InlineData("railway")]
    [InlineData("pilot")]
    public void AFilledDial_BringsItsOwnColors_WithGoodContrast(string id)
    {
        var preset = Preset(id);
        var fill = Rgb.Parse(preset.Style.DialFill!);
        var colors = preset.Colors!.Value;

        double Contrast(string? hex) => Rgb.Contrast(Rgb.Parse(hex!), fill);
        Assert.True(Contrast(colors.Numerals) >= 4.5 || preset.Style.Numerals == NumeralStyle.None, $"{id} numerals");
        Assert.True(Contrast(colors.Hour) >= 4.5, $"{id} hour hand");
        Assert.True(Contrast(colors.Minute) >= 4.5, $"{id} minute hand");
        Assert.True(Contrast(colors.Second) >= 4.5, $"{id} second hand");
        Assert.True(Contrast(colors.Markers) >= 3, $"{id} markers");
        Assert.True(Contrast(colors.Dial) >= 3, $"{id} rim");
    }

    [Fact]
    public void TheThemeFaces_FollowTheTheme_SoTheyCarryNoColorsOfTheirOwn()
    {
        Assert.Null(Preset("slim").Colors);
        Assert.Null(Preset("hours24").Colors);
        Assert.Null(Preset("slim").Style.DialFill);
    }

    [Fact]
    public void Applying_AFace_ReplacesTheColors_WithTheFacesOwn_OrTheTheme()
    {
        var current = ClockStyle.Default with { Alignment = ClockAlignment.Left, ShowSeconds = false, Stopwatch = StopwatchDisplay.Analog, Colors = new ClockColors("#112233", null, null, null, null, null) };

        var classic = ClockPresets.Apply("classic", current);
        var slim = ClockPresets.Apply("slim", current);

        Assert.Equal(Preset("classic").Colors, classic.Colors);
        Assert.Equal(ClockColors.Theme, slim.Colors); // the user's colors belong to no face, so they do not carry onto one
        Assert.All(new[] { classic, slim }, s =>
        {
            Assert.Equal(ClockAlignment.Left, s.Alignment);
            Assert.False(s.ShowSeconds);
            Assert.Equal(StopwatchDisplay.Analog, s.Stopwatch); // the stopwatch display is the user's choice, not the look's
        });
    }

    [Fact]
    public void EveryFace_IsRecognizedAfterItIsApplied_AndACustomisedLookIsNot()
    {
        foreach (var preset in ClockPresets.All)
        {
            Assert.Equal(preset.Id, ClockPresets.IdOf(ClockPresets.Apply(preset.Id, ClockStyle.Default)));
        }
        Assert.Null(ClockPresets.IdOf(ClockPresets.Apply("pilot", ClockStyle.Default) with { Tachymeter = true }));
    }

    [Fact]
    public void TheTwentyFourHourHand_GoesOnceAroundADay()
    {
        Assert.Equal(0, ClockMath.Hour24Angle(new TimeOnly(0, 0)), 6);
        Assert.Equal(90, ClockMath.Hour24Angle(new TimeOnly(6, 0)), 6);
        Assert.Equal(180, ClockMath.Hour24Angle(new TimeOnly(12, 0)), 6);
        Assert.Equal(270, ClockMath.Hour24Angle(new TimeOnly(18, 0)), 6);
        Assert.Equal(187.5, ClockMath.Hour24Angle(new TimeOnly(12, 30)), 6);
        Assert.Equal(15 * 23.99, ClockMath.Hour24Angle(new TimeOnly(23, 59, 24)), 1);
    }

    [Fact]
    public void TheTwentyFourHourLabels_SitEveryThreeHours_FromMidnightAtTheTop()
    {
        var labels = ClockLabels.TwentyFour();

        Assert.Equal(new[] { "24", "3", "6", "9", "12", "15", "18", "21" }, labels.Select(l => l.Text).ToArray());
        Assert.Equal(new double[] { 0, 45, 90, 135, 180, 225, 270, 315 }, labels.Select(l => ClockLabels.AngleOf24(l.Hour)).ToArray());
    }

    [Fact]
    public void TheTachymeterScale_PutsSixtyAtTheTop_AndHigherSpeedsFurtherAround()
    {
        var marks = ClockTachymeter.Marks;

        Assert.Equal(60, marks[0].Speed);
        Assert.Equal(0, marks[0].Angle, 6); // 60 units per hour: one second per unit
        var eighty = marks.Single(m => m.Speed == 80);
        Assert.Equal(270, eighty.Angle, 6); // 3600 / 80 = 45 seconds
        Assert.Equal(180, marks.Single(m => m.Speed == 120).Angle, 6); // 30 seconds
        Assert.True(marks.All(m => m.Angle is >= 0 and < 360));
        Assert.Equal(marks.OrderBy(m => m.Speed).Select(m => m.Speed), marks.Select(m => m.Speed)); // in order of speed
    }

    [Fact]
    public void TheNewOptions_AreKeptAndCleaned_WhenAStyleIsNormalized()
    {
        var style = ClockStyle.Default with { DialFill = "#abcdef", Kind = DialKind.Chronograph, Tachymeter = true, MinuteTrack = true, Stopwatch = StopwatchDisplay.Analog };

        var normalized = (style with { DialFill = "nonsense" }).Normalized();

        Assert.Null(normalized.DialFill);
        Assert.Equal("#ABCDEF", style.Normalized().DialFill);
        Assert.Equal(DialKind.Chronograph, normalized.Kind);
        Assert.True(normalized.Tachymeter && normalized.MinuteTrack);
        Assert.Equal(StopwatchDisplay.Analog, normalized.Stopwatch);
    }

    [Fact]
    public void AFaceWithTheNewOptions_SurvivesSaving()
    {
        var style = ClockPresets.Apply("chrono", ClockStyle.Default) with { Stopwatch = StopwatchDisplay.Analog };
        var face = new CustomClockFace(Guid.NewGuid(), "Chrono mine", style, ClockSlotsConfig.Default);

        var back = Assert.Single(ClockFaceJson.Parse(ClockFaceJson.Serialize([face])));

        Assert.Equal(style, back.Style);
        Assert.Equal(DialKind.Chronograph, back.Style.Kind);
        Assert.Equal(StopwatchDisplay.Analog, back.Style.Stopwatch);
    }

    [Fact]
    public void AnOldSavedFace_WithoutTheNewFields_StillLoadsWithPlainDefaults()
    {
        var json = "{\"version\":1,\"faces\":[{\"id\":\"" + Guid.NewGuid() + "\",\"name\":\"Old\",\"style\":{\"shape\":\"Round\",\"markers\":\"Ticks\"},\"slots\":{\"slots\":[]}}]}";

        var face = Assert.Single(ClockFaceJson.Parse(json));

        Assert.Equal(DialKind.Standard, face.Style.Kind);
        Assert.Null(face.Style.DialFill);
        Assert.False(face.Style.Tachymeter);
        Assert.Equal(StopwatchDisplay.Digital, face.Style.Stopwatch);
    }
}
