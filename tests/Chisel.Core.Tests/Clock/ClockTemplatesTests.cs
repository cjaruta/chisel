using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

// Every built-in face is a self-contained, immutable definition. Choosing one puts the whole definition on, so nothing of the face
// that was there before (its colors, markers, hands, dial fill, glow) is left on the new one. The only things a face does not own
// are the user's own preferences: alignment, whether the seconds hand shows, and the stopwatch display.
public class ClockTemplatesTests
{
    private static readonly string[] Ids = ClockPresets.All.Select(p => p.Id).ToArray();

    // Everything about a style except the user's preferences.
    private static ClockStyle Look(ClockStyle style) => style.WithoutPreferences();

    private static ClockPreset Template(string id) => ClockPresets.All.Single(p => p.Id == id);

    private static void AssertIsExactly(string id, ClockStyle active)
    {
        Assert.Equal(Look(Template(id).Style), Look(active));
        Assert.Equal(id, ClockPresets.IdOf(active));
    }

    // A working configuration with something odd in every part, as it would be after a lot of fiddling or after another face.
    private static ClockStyle Messy => new ClockStyle
    {
        Alignment = ClockAlignment.Right, Shape = DialShape.Square, BorderWidth = 7, Markers = MarkerStyle.Lume, EmphasizeQuarters = false,
        Numerals = NumeralStyle.Roman, Hands = HandStyle.Sword, HourThickness = 9, MinuteThickness = 8, SecondThickness = 6, ShowSeconds = false,
        Colors = new ClockColors("#112233", "#445566", "#778899", "#AABBCC", "#DDEEFF", "#FF00FF"),
        Kind = DialKind.Chronograph, DialFill = "#00FF00", MinuteTrack = true, Tachymeter = true, Stopwatch = StopwatchDisplay.Analog,
    };

    [Fact]
    public void EveryFace_Applied_ToAnythingAtAll_IsExactlyItsDefinition()
    {
        foreach (var start in new[] { ClockStyle.Default, Messy })
        {
            foreach (var id in Ids)
            {
                AssertIsExactly(id, ClockPresets.Apply(id, start));
            }
        }
    }

    [Fact]
    public void GoingFromOneFaceToAnotherAndBack_LeavesNothingBehind()
    {
        foreach (var a in Ids)
        {
            foreach (var b in Ids)
            {
                var style = ClockPresets.Apply(a, Messy);
                AssertIsExactly(a, style);
                style = ClockPresets.Apply(b, style);
                AssertIsExactly(b, style);
                style = ClockPresets.Apply(a, style);
                AssertIsExactly(a, style); // A -> B -> A is A's own default look again
            }
        }
    }

    [Fact]
    public void ClickingThroughEveryFaceInOrder_ShowsEachOneAsItsDefinition()
    {
        var style = Messy;
        foreach (var id in Ids.Concat(Ids.Reverse()))
        {
            style = ClockPresets.Apply(id, style);
            AssertIsExactly(id, style);
        }
    }

    [Fact]
    public void ClickingThroughTheFacesInRandomOrders_ShowsEachOneAsItsDefinition()
    {
        foreach (var seed in new[] { 1, 7, 42, 2026, 31337 })
        {
            var random = new Random(seed);
            var style = ClockStyle.Default;
            for (var i = 0; i < 200; i++)
            {
                var id = Ids[random.Next(Ids.Length)];
                style = ClockPresets.Apply(id, style);
                AssertIsExactly(id, style);
            }
        }
    }

    [Theory]
    [InlineData("pilot", "slim")] // the glow-in-the-dark look onto a plain one
    [InlineData("classic", "numbers")] // the white dial and dark ink onto a themed one
    [InlineData("chrono", "minimal")] // sub-dials, tachymeter and a dark fill onto the simplest face
    [InlineData("railway", "dots")]
    [InlineData("hours24", "roman")]
    public void NothingOfTheOldFace_RemainsOnTheNewOne(string from, string to)
    {
        var after = ClockPresets.Apply(to, ClockPresets.Apply(from, ClockStyle.Default));
        var target = Template(to).Style;

        Assert.Equal(target.Colors, after.Colors);
        Assert.Equal(target.DialFill, after.DialFill);
        Assert.Equal(target.Markers, after.Markers);
        Assert.Equal(target.Hands, after.Hands);
        Assert.Equal(target.Kind, after.Kind);
        Assert.Equal(target.MinuteTrack, after.MinuteTrack);
        Assert.Equal(target.Tachymeter, after.Tachymeter);
        Assert.Equal(target.Numerals, after.Numerals);
        Assert.Equal(target.Shape, after.Shape);
        Assert.Equal(target.BorderWidth, after.BorderWidth);
        Assert.Equal(target.HourThickness, after.HourThickness);
        Assert.Equal(target.MinuteThickness, after.MinuteThickness);
        Assert.Equal(target.SecondThickness, after.SecondThickness);
        Assert.Equal(target.EmphasizeQuarters, after.EmphasizeQuarters);
    }

    [Fact]
    public void AFaceWithoutColorsOfItsOwn_WearsTheThemeColors_NotThePreviousFacesOrThePalettes()
    {
        var pilot = ClockPresets.Apply("pilot", ClockStyle.Default);
        Assert.NotEqual(ClockColors.Theme, pilot.Colors);

        Assert.Equal(ClockColors.Theme, ClockPresets.Apply("slim", pilot).Colors);
        var amber = ClockPalettes.Apply("amber", ClockStyle.Default);
        Assert.Equal(ClockColors.Theme, ClockPresets.Apply("minimal", amber).Colors);
    }

    [Fact]
    public void ThePreferences_AreTheUsers_AndSurviveEveryFace()
    {
        var mine = ClockStyle.Default with { Alignment = ClockAlignment.Left, ShowSeconds = false, Stopwatch = StopwatchDisplay.Analog };

        foreach (var id in Ids)
        {
            var applied = ClockPresets.Apply(id, mine);
            Assert.Equal(ClockAlignment.Left, applied.Alignment);
            Assert.False(applied.ShowSeconds);
            Assert.Equal(StopwatchDisplay.Analog, applied.Stopwatch);
        }
    }

    [Fact]
    public void ChangesToTheWorkingStyle_NeverTouchTheTemplates()
    {
        var before = ClockPresets.All.ToList();
        var snapshot = before.Select(p => (p.Id, p.Title, p.Style, p.Colors)).ToList();

        // Customize in every way the settings allow, on top of a face.
        var working = ClockPresets.Apply("pilot", ClockStyle.Default);
        working = ClockPalettes.Apply("ocean", working) with
        {
            HourThickness = 10, DialFill = "#123456", Markers = MarkerStyle.Dots, Hands = HandStyle.Leaf, Tachymeter = true, Kind = DialKind.TwentyFourHour,
        };
        working = working.Normalized();

        Assert.Equal(snapshot, ClockPresets.All.Select(p => (p.Id, p.Title, p.Style, p.Colors)).ToList());
        Assert.Equal(before.Count, ClockPresets.All.Count);
        Assert.Null(ClockPresets.IdOf(working)); // it is a custom look now
        AssertIsExactly("pilot", ClockPresets.Apply("pilot", working)); // and the face is still its own default
    }

    [Fact]
    public void TheTemplateList_CannotBeChangedByAnyoneHoldingIt()
    {
        var all = ClockPresets.All;

        Assert.True(all is not System.Collections.IList { IsReadOnly: false });
        Assert.True(all is not System.Collections.Generic.ICollection<ClockPreset> { IsReadOnly: false });
        Assert.Same(ClockPresets.All, ClockPresets.All); // one definition, shared read-only
    }

    [Fact]
    public void EveryTemplate_OwnsItsColorsInItsStyle_SoThereIsNothingElseToLeak()
    {
        foreach (var preset in ClockPresets.All)
        {
            // A face with a solid dial brings its own colors; a theme face has none (it follows the theme). Either way the whole
            // definition is the preset's style.
            Assert.Equal(preset.Style.DialFill is not null, preset.Style.Colors != ClockColors.Theme);
            Assert.Equal(preset.Style.Colors == ClockColors.Theme ? null : preset.Style.Colors, preset.Colors);
        }
    }
}
