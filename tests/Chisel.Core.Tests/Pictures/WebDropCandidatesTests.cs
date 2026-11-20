using System.Text;
using Chisel.Core.Pictures;

namespace Chisel.Core.Tests.Pictures;

// Browser drags: the CF_HTML clipboard format, srcset, and image-search links that wrap the real picture address.
public class WebDropCandidatesTests
{
    private static string[] Addresses(IReadOnlyList<PictureCandidate> candidates) => candidates.Select(c => c.Address).ToArray();

    // Builds a CF_HTML block the way Chromium does: a header with byte offsets, then the document with a fragment marked inside.
    private static byte[] CfHtml(string fragment, string sourceUrl, string outside = "")
    {
        const string template = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\nSourceURL:{4}\r\n";
        var header = string.Format(template, 0, 0, 0, 0, sourceUrl);
        var start = Encoding.UTF8.GetByteCount(header);
        var before = "<html><body>" + outside + "<!--StartFragment-->";
        var after = "<!--EndFragment--></body></html>";
        var startFragment = start + Encoding.UTF8.GetByteCount(before);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var end = endFragment + Encoding.UTF8.GetByteCount(after);
        header = string.Format(template, start, end, startFragment, endFragment, sourceUrl);
        return Encoding.UTF8.GetBytes(header + before + fragment + after);
    }

    [Fact]
    public void AClipboardHtmlBlock_GivesItsFragmentAndPageAddress()
    {
        var bytes = CfHtml("<img src=\"/img/é.png\">", "https://example.com/gallery/");

        var html = HtmlClipboard.Decode(bytes);

        Assert.NotNull(html);
        var candidates = PictureCandidates.Build(html, [], []);
        Assert.Equal(new[] { "https://example.com/img/%C3%A9.png" }, Addresses(candidates));
        Assert.Equal("https://example.com/gallery/", candidates[0].Referer);
    }

    [Fact]
    public void OnlyTheFragmentCounts_NotOtherPicturesOnThePage()
    {
        var bytes = CfHtml("<img src=\"https://cdn.example.com/a.png\">", "https://example.com/p", "<img src=\"https://cdn.example.com/other.png\">");

        Assert.Equal(new[] { "https://cdn.example.com/a.png" }, Addresses(PictureCandidates.Build(HtmlClipboard.Decode(bytes), [], [])));
    }

    [Fact]
    public void PlainUtf16Html_IsDecodedToo()
    {
        var bytes = Encoding.Unicode.GetBytes("<img src=\"https://cdn.example.com/a.png\">\0");

        Assert.Equal(new[] { "https://cdn.example.com/a.png" }, Addresses(PictureCandidates.Build(HtmlClipboard.Decode(bytes), [], [])));
    }

    [Fact]
    public void ASrcsetsFirstEntry_FollowsTheSrc_AndDataImagesCount()
    {
        var html = "<img src=\"https://cdn.example.com/a.png\" srcset=\"https://cdn.example.com/a2.png 2x, https://cdn.example.com/a3.png 3x\">"
            + "<img src=\"data:image/png;base64,AAAA\">";

        Assert.Equal(new[] { "https://cdn.example.com/a.png", "https://cdn.example.com/a2.png", "data:image/png;base64,AAAA" },
            Addresses(PictureCandidates.Build(html, [], [])));
    }

    [Fact]
    public void AGoogleImgresLink_GivesItsImgurlFirst_ThenTheLink()
    {
        var link = "https://www.google.com/imgres?imgurl=https%3A%2F%2Fcdn.example.com%2Fcat.jpg%3Fw%3D1&imgrefurl=https%3A%2F%2Fexample.com%2Farticle&tbnid=x";

        Assert.Equal(new[] { "https://cdn.example.com/cat.jpg?w=1", link }, Addresses(PictureCandidates.Build(null, [link], [])));
    }

    [Fact]
    public void AnyQueryValueThatIsAnImageAddress_IsUnwrapped_ButAPageAddressIsNot()
    {
        var wrapped = "https://redirect.example.net/go?u=https%3A%2F%2Fcdn.example.com%2Fa.WEBP&back=https%3A%2F%2Fexample.com%2Fpage";

        Assert.Equal(new[] { "https://cdn.example.com/a.WEBP", wrapped }, Addresses(PictureCandidates.Build(null, [], [wrapped])));
    }

    [Fact]
    public void TheOrder_IsHtmlImages_ThenUnwrapped_ThenLinks_ThenText()
    {
        var link = "https://www.google.com/imgres?imgurl=https%3A%2F%2Fcdn.example.com%2Fcat.jpg";

        var candidates = PictureCandidates.Build("<img src=\"https://cdn.example.com/inline.png\">", [link, "https://example.com/article"], ["https://example.com/t.png"]);

        Assert.Equal(new[]
        {
            "https://cdn.example.com/inline.png", "https://cdn.example.com/cat.jpg", link, "https://example.com/article", "https://example.com/t.png",
        }, Addresses(candidates));
    }

    [Fact]
    public void AFileDescriptor_GivesTheFirstFileName()
    {
        var bytes = new byte[4 + 592 * 2];
        BitConverter.GetBytes(2).CopyTo(bytes, 0);
        Encoding.Unicode.GetBytes("cat.png").CopyTo(bytes, 4 + 72);
        Encoding.Unicode.GetBytes("dog.png").CopyTo(bytes, 4 + 592 + 72);

        Assert.Equal(new[] { "cat.png", "dog.png" }, FileDescriptors.Names(bytes));
        Assert.Empty(FileDescriptors.Names([1, 2, 3]));
    }
}
