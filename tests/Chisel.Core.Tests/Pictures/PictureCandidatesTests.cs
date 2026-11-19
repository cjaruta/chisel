using Chisel.Core.Pictures;

namespace Chisel.Core.Tests.Pictures;

public class PictureCandidatesTests
{
    private static string[] Addresses(IReadOnlyList<PictureCandidate> candidates) => candidates.Select(c => c.Address).ToArray();

    [Fact]
    public void ImgSources_ComeFirst_ThenUriListEntries_ThenPlainText_WithoutRepeats()
    {
        var candidates = PictureCandidates.Build(
            "<p><img src=\"https://cdn.example.com/a.png\"><img src='https://cdn.example.com/b.webp'></p>",
            ["https://example.com/page\r\n# comment\r\nhttps://cdn.example.com/a.png"],
            ["https://example.com/text.jpg", "https://example.com/page"]);

        Assert.Equal(new[]
        {
            "https://cdn.example.com/a.png", "https://cdn.example.com/b.webp", "https://example.com/page", "https://example.com/text.jpg",
        }, Addresses(candidates));
    }

    [Fact]
    public void ARelativeSrc_IsResolvedAgainstThePageAddress()
    {
        var html = "Version:0.9\r\nSourceURL:https://example.com/gallery/index.html\r\n<html><body><!--StartFragment--><img src=\"../img/cat.png\"><!--EndFragment--></body></html>";

        var candidates = PictureCandidates.Build(html, [], []);

        Assert.Equal(new[] { "https://example.com/img/cat.png" }, Addresses(candidates));
        Assert.Equal("https://example.com/gallery/index.html", candidates[0].Referer);
    }

    [Fact]
    public void ARootRelativeAndAProtocolRelativeSrc_AreResolved_AndABaseTagWins()
    {
        var page = "https://example.com/a/b/page.html";
        Assert.Equal(new[] { "https://example.com/x.png" }, Addresses(PictureCandidates.Build("<img src=\"/x.png\">", [], [], page)));
        Assert.Equal(new[] { "https://cdn.example.org/y.png" }, Addresses(PictureCandidates.Build("<img src=\"//cdn.example.org/y.png\">", [], [], page)));
        Assert.Equal(new[] { "https://static.example.com/assets/z.png" },
            Addresses(PictureCandidates.Build("<base href=\"https://static.example.com/assets/\"><img src=\"z.png\">", [], [], page)));
    }

    [Fact]
    public void ARelativeSrcWithNoPageAddress_CannotBeUsed()
    {
        Assert.Empty(PictureCandidates.Build("<img src=\"cat.png\">", [], []));
    }

    [Fact]
    public void ADataImage_IsACandidate_AndOtherSchemesAreNot()
    {
        var candidates = PictureCandidates.Build(
            "<img src=\"data:image/png;base64,AAAA\"><img src=\"file:///c:/x.png\"><img src=\"javascript:alert(1)\">",
            ["ftp://example.com/a.png"], ["data:image/svg+xml;base64,AAAA"]);

        Assert.Equal(new[] { "data:image/png;base64,AAAA" }, Addresses(candidates));
    }

    [Fact]
    public void TheRefererIsThePageAddress_WhenKnown_AndNeverADataAddress()
    {
        var html = "SourceURL:https://example.com/page\r\n<img src=\"https://cdn.example.com/a.png\"><img src=\"data:image/png;base64,AAAA\">";

        var candidates = PictureCandidates.Build(html, ["https://cdn.example.com/b.png"], []);

        Assert.Equal("https://example.com/page", candidates[0].Referer);
        Assert.Equal("https://example.com/page", candidates[2].Referer);
        Assert.Null(candidates[1].Referer);
    }

    [Fact]
    public void WithoutAPageAddress_TheRefererIsLeftOutForTheDownloaderToChoose()
    {
        var candidates = PictureCandidates.Build("<img src=\"https://cdn.example.com/a.png\">", [], []);

        Assert.Null(candidates.Single().Referer);
    }

    [Fact]
    public void AnEntityInTheAddress_IsDecoded_AndNothingGivesNothing()
    {
        Assert.Equal(new[] { "https://example.com/a.png?x=1&y=2" },
            Addresses(PictureCandidates.Build("<img src=\"https://example.com/a.png?x=1&amp;y=2\">", [], [])));
        Assert.Empty(PictureCandidates.Build(null, [], []));
        Assert.Empty(PictureCandidates.Build("<p>no picture</p>", [null, ""], [" "]));
    }
}

public class ImageProblemTests
{
    private static readonly byte[] Html = "<!DOCTYPE html><html><head><title>Not found</title></head></html>"u8.ToArray();

    [Fact]
    public void AWebPage_IsToldApartFromAPicture_ByItsBytesOrItsContentType()
    {
        Assert.Equal(ImageProblem.WebPage, ImageSniffer.Classify(Html, null));
        Assert.Equal(ImageProblem.WebPage, ImageSniffer.Classify("  \n<html><body>hi</body></html>"u8, null));
        Assert.Equal(ImageProblem.WebPage, ImageSniffer.Classify([1, 2, 3], "text/html; charset=utf-8"));
    }

    [Fact]
    public void AvifHeicSvgAndTheLike_AreUnsupported_NotBroken()
    {
        var avif = new byte[] { 0, 0, 0, 0x1C, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'a', (byte)'v', (byte)'i', (byte)'f', 0, 0, 0, 0 };
        var heic = new byte[] { 0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'h', (byte)'e', (byte)'i', (byte)'c', 0, 0, 0, 0 };
        Assert.Equal(ImageProblem.Unsupported, ImageSniffer.Classify(avif, null));
        Assert.Equal(ImageProblem.Unsupported, ImageSniffer.Classify(heic, null));
        Assert.Equal(ImageProblem.Unsupported, ImageSniffer.Classify([9, 9, 9], "image/avif"));
        Assert.Equal(ImageProblem.Unsupported, ImageSniffer.Classify("<svg xmlns='http://www.w3.org/2000/svg'/>"u8, null));
        Assert.Equal(ImageProblem.Unsupported, ImageSniffer.Classify("<?xml version=\"1.0\"?><svg/>"u8, "image/svg+xml"));
        Assert.Equal(ImageProblem.Unsupported, ImageSniffer.Classify([0x42, 0x4D, 0, 0, 0, 0], "image/bmp"));
    }

    [Fact]
    public void Anything_Else_IsNotAnImage()
    {
        Assert.Equal(ImageProblem.NotAnImage, ImageSniffer.Classify([1, 2, 3, 4, 5, 6, 7, 8], "application/octet-stream"));
        Assert.Equal(ImageProblem.NotAnImage, ImageSniffer.Classify([], null));
    }

    [Fact]
    public void EachProblem_HasItsOwnPlainMessage()
    {
        Assert.Equal("That link is a web page, not an image", ImageProblems.Message(ImageProblem.WebPage));
        Assert.Equal("AVIF and other formats are not supported", ImageProblems.Message(ImageProblem.Unsupported));
        Assert.Equal("Not an image", ImageProblems.Message(ImageProblem.NotAnImage));
    }

    [Fact]
    public void TheImporter_UsesThem_ForFilesToo()
    {
        var importer = new PictureImporter(new FakeRepository(), new FakeStore(), new NoResizer(), TimeProvider.System);

        Assert.Equal("Not an image", importer.Prepare([1, 2, 3, 4], "a.png", null, true).Error);
        Assert.Equal("That link is a web page, not an image", importer.Prepare(Html, "a.png", null, true).Error);
        Assert.Equal("AVIF and other formats are not supported", importer.Prepare("<svg/>"u8.ToArray(), "a.svg", null, true).Error);
    }

    private sealed class NoResizer : IImageResizer
    {
        public byte[]? Resize(byte[] data, int width, int height) => null;
    }
}
