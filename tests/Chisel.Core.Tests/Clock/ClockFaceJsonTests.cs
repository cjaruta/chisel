using Chisel.Core.Clock;

namespace Chisel.Core.Tests.Clock;

public class ClockFaceJsonTests
{
    private static CustomClockFace Face(string name, ClockStyle? style = null, ClockSlotsConfig? slots = null) =>
        new(Guid.NewGuid(), name, style ?? ClockPresets.All[1].Style with { Colors = new ClockColors("#112233", null, "#AABBCC", null, null, "#FF0000") },
            slots ?? new([ClockSlotConfig.Pinned(ClockTool.Clock), ClockSlotConfig.Scrolling(ClockTool.Timer, ClockTool.Stopwatch)]));

    [Fact]
    public void AllFacesSurviveARoundTrip_WithEveryStyleOptionAndTheLayout()
    {
        var faces = new[] { Face("Day"), Face("Night", ClockStyle.Default with { Alignment = ClockAlignment.Right, ShowSeconds = false, Shape = DialShape.RoundedSquare, Hands = HandStyle.Leaf }) };

        var back = ClockFaceJson.Parse(ClockFaceJson.Serialize(faces));

        Assert.Equal(2, back.Count);
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(faces[i].Id, back[i].Id);
            Assert.Equal(faces[i].Name, back[i].Name);
            Assert.Equal(faces[i].Style, back[i].Style);
            Assert.Equal(faces[i].Slots, back[i].Slots);
        }
    }

    [Fact]
    public void TheFileCarriesAVersion()
    {
        var json = ClockFaceJson.Serialize([Face("Day")]);

        Assert.Matches("\"version\":\\s*1", json);
        Assert.Equal(1, ClockFaceJson.CurrentVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"faces\":null}")]
    [InlineData("{\"version\":1,\"faces\":\"oops\"}")]
    public void MissingOrCorruptData_GivesNoFaces_AndNeverThrows(string? json)
    {
        Assert.Empty(ClockFaceJson.Parse(json));
    }

    [Fact]
    public void OneBadFace_IsSkipped_AndTheGoodOnesStay()
    {
        var text = "{\"version\":1,\"faces\":["
            + "{\"id\":\"" + Guid.NewGuid() + "\",\"name\":\"Bad shape\",\"style\":{\"shape\":\"Hexagon\"},\"slots\":{}},"
            + "{\"id\":\"" + Guid.NewGuid() + "\",\"name\":\"\"},"
            + "7,"
            + "{\"id\":\"" + Guid.NewGuid() + "\",\"name\":\"Good\",\"style\":{\"shape\":\"Square\"},\"slots\":{\"slots\":[{\"isScrolling\":false,\"tool\":\"Clock\",\"set\":\"None\"}]}}"
            + "]}";

        var back = ClockFaceJson.Parse(text);

        var good = Assert.Single(back);
        Assert.Equal("Good", good.Name);
        Assert.Equal(DialShape.Square, good.Style.Shape);
        Assert.Single(good.Slots.Slots);
    }

    [Fact]
    public void OutOfRangeValues_AreClamped_AndBadColorsBecomeTheThemeColor()
    {
        var text = "{\"version\":1,\"faces\":[{\"id\":\"" + Guid.NewGuid() + "\",\"name\":\"Wild\","
            + "\"style\":{\"borderWidth\":500,\"hourThickness\":-9,\"colors\":{\"dial\":\"red!\",\"hour\":\"#abcdef\"}},"
            + "\"slots\":{\"slots\":[]}}]}";

        var face = Assert.Single(ClockFaceJson.Parse(text));

        Assert.Equal(ClockStyle.MaxBorder, face.Style.BorderWidth);
        Assert.Equal(ClockStyle.MinThickness, face.Style.HourThickness);
        Assert.Null(face.Style.Colors.Dial);
        Assert.Equal("#ABCDEF", face.Style.Colors.Hour);
        Assert.Single(face.Slots.Slots); // no slots at all becomes the clock alone
    }

    [Fact]
    public void DuplicateNamesAndIds_AreMadeUnique()
    {
        var a = Face("Same");
        var back = ClockFaceJson.Parse(ClockFaceJson.Serialize([a, a]));

        Assert.Equal(2, back.Count);
        Assert.Equal(2, back.Select(f => f.Id).Distinct().Count());
        Assert.Equal(2, back.Select(f => f.Name.ToLowerInvariant()).Distinct().Count());
    }

    [Fact]
    public void AFileFromANewerVersion_StillLoadsWhatIsUnderstood()
    {
        var json = System.Text.RegularExpressions.Regex.Replace(ClockFaceJson.Serialize([Face("Future")]), "\"version\":\\s*1", "\"version\": 99");

        var back = ClockFaceJson.Parse(json);

        Assert.Equal(new[] { "Future" }, back.Select(f => f.Name).ToArray());
    }

    [Fact]
    public void OneFace_CanBeExportedAndImported_AsItsOwnFile()
    {
        var face = Face("Share me");

        var text = ClockFaceJson.SerializeOne(face);
        var ok = ClockFaceJson.TryParseOne(text, out var back);

        Assert.True(ok);
        Assert.Equal(face.Name, back!.Name);
        Assert.Equal(face.Style, back.Style);
        Assert.Equal(face.Slots, back.Slots);
        Assert.False(ClockFaceJson.TryParseOne("garbage", out _));
        Assert.False(ClockFaceJson.TryParseOne(null, out _));
    }
}
