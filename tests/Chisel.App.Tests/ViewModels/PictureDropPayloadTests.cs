using Avalonia.Input;
using Chisel.App.Services;
using Chisel.Core.Pictures;

namespace Chisel.App.Tests.ViewModels;

// What a drop carries, in order of preference: files from Explorer, then bitmap data, then the addresses a browser gives
// (an img src in HTML first, then a URI list, then plain text), all as the candidates of one picture.
public class PictureDropPayloadTests
{
    private static readonly byte[] Png = [1, 2, 3];

    private sealed class MemoryLog : IPictureDropLog
    {
        public List<string> Lines { get; } = [];
        public void Write(string line) => Lines.Add(line);
    }

    private static string[] Addresses(IReadOnlyList<PictureSource> sources) =>
        sources.Cast<WebSource>().Single().Candidates.Select(c => c.Address).ToArray();

    [Fact]
    public void Files_BeatEverythingElse()
    {
        var sources = PictureDropPayload.Choose(["C:\\a\\one.png", "C:\\a\\two.jpg"], Png, ["https://example.com/x.png"], "<img src=\"https://example.com/y.png\">");

        Assert.Equal(new PictureSource[] { new FileSource("C:\\a\\one.png"), new FileSource("C:\\a\\two.jpg") }, sources);
    }

    [Fact]
    public void ABitmap_BeatsAnAddress()
    {
        var sources = PictureDropPayload.Choose([], Png, ["https://example.com/x.png"], null);

        var bitmap = Assert.IsType<BytesSource>(Assert.Single(sources));
        Assert.Equal(Png, bitmap.Bytes);
        Assert.EndsWith(".png", bitmap.Name);
    }

    [Fact]
    public void AnImgSrcInHtml_IsTriedBeforeTheLinkAroundIt_AllAsOnePictureWithCandidates()
    {
        var sources = PictureDropPayload.Choose([], null, ["https://example.com/page"],
            "<a href=\"https://example.com/page\"><img src=\"https://example.com/cat.png\"></a>");

        Assert.Equal(new[] { "https://example.com/cat.png", "https://example.com/page" }, Addresses(sources));
    }

    [Fact]
    public void ARelativeSrc_IsResolvedAgainstThePageAddressInTheHtmlHeader_AndThePageIsTheReferer()
    {
        var html = "Version:0.9\r\nSourceURL:https://example.com/gallery/index.html\r\n<img src=\"../img/cat.webp\">";

        var source = Assert.IsType<WebSource>(Assert.Single(PictureDropPayload.Choose([], null, [], html)));

        Assert.Equal("https://example.com/img/cat.webp", source.Candidates[0].Address);
        Assert.Equal("https://example.com/gallery/index.html", source.Candidates[0].Referer);
    }

    [Fact]
    public void UriListEntries_ThenPlainText_AreCandidatesToo_WithoutRepeats()
    {
        var sources = PictureDropPayload.Choose([], null, ["https://example.com/text.jpg", "https://example.com/a.png"], null,
            ["https://example.com/a.png\r\n# comment\r\nhttp://example.com/b.png\r\nftp://example.com/c.png"]);

        Assert.Equal(new[] { "https://example.com/a.png", "http://example.com/b.png", "https://example.com/text.jpg" }, Addresses(sources));
    }

    [Theory]
    [InlineData("file:///c:/secret.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("just some words")]
    [InlineData("")]
    public void OtherText_IsNotAPicture(string text) =>
        Assert.Empty(PictureDropPayload.Choose([], null, [text], null));

    [Fact]
    public void ADataImage_IsACandidate() =>
        Assert.Equal(new[] { "data:image/png;base64,AAAA" }, Addresses(PictureDropPayload.Choose([], null, ["data:image/png;base64,AAAA"], null)));

    [Fact]
    public void NothingUsable_GivesNothing() => Assert.Empty(PictureDropPayload.Choose([], null, [], null));

    [Fact]
    public void ReadsTextFromARealDataTransfer()
    {
        var item = new DataTransferItem();
        item.SetText("https://example.com/a.png");
        var data = new DataTransfer();
        data.Add(item);

        Assert.True(PictureDropPayload.HasPictures(data));
        Assert.Equal(new[] { "https://example.com/a.png" }, Addresses(PictureDropPayload.Read(data)));
    }

    [Fact]
    public void ARealDataTransferWithSomethingElse_HasNoPictures()
    {
        var item = new DataTransferItem();
        item.SetText("hello");
        var data = new DataTransfer();
        data.Add(item);

        Assert.False(PictureDropPayload.HasPictures(data));
        Assert.Empty(PictureDropPayload.Read(data));
    }

    [Fact]
    public void EveryDrop_LogsTheFormatsPresent_AndTheCandidatesFound()
    {
        var item = new DataTransferItem();
        item.SetText("https://example.com/a.png");
        var data = new DataTransfer();
        data.Add(item);
        var log = new MemoryLog();

        PictureDropPayload.Read(data, log);

        var text = string.Join("\n", log.Lines);
        Assert.Contains("drop formats:", text);
        Assert.Contains("drop candidates (1): https://example.com/a.png", text);
    }

    [Fact]
    public void ADropWithNoPicture_LogsThatToo()
    {
        var item = new DataTransferItem();
        item.SetText("hello");
        var data = new DataTransfer();
        data.Add(item);
        var log = new MemoryLog();

        PictureDropPayload.Read(data, log);

        Assert.Contains(log.Lines, l => l.Contains("drop candidates (0)"));
    }
}
