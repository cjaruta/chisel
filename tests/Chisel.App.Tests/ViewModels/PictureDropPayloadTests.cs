using Avalonia.Input;
using Chisel.App.Services;

namespace Chisel.App.Tests.ViewModels;

// What a drop carries, in order of preference: files from Explorer, then bitmap data, then an image address.
public class PictureDropPayloadTests
{
    private static readonly byte[] Png = [1, 2, 3];

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
    public void AnImgSrcInHtml_IsPreferredToTheLinkAroundIt()
    {
        var sources = PictureDropPayload.Choose([], null, ["https://example.com/page"],
            "<a href=\"https://example.com/page\"><img src=\"https://example.com/cat.png\"></a>");

        Assert.Equal(new PictureSource[] { new UrlSource("https://example.com/cat.png") }, sources);
    }

    [Fact]
    public void ADraggedLink_IsAnAddress_EachLineOfAListToo()
    {
        Assert.Equal(new PictureSource[] { new UrlSource("https://example.com/a.png") },
            PictureDropPayload.Choose([], null, ["https://example.com/a.png"], null));
        Assert.Equal(new PictureSource[] { new UrlSource("http://example.com/a.png"), new UrlSource("https://example.com/b.png") },
            PictureDropPayload.Choose([], null, ["http://example.com/a.png\r\n# comment\r\nhttps://example.com/b.png\r\nftp://example.com/c.png"], null));
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
    public void ADataImage_IsAnAddress() =>
        Assert.Single(PictureDropPayload.Choose([], null, ["data:image/png;base64,AAAA"], null));

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
        Assert.Equal(new PictureSource[] { new UrlSource("https://example.com/a.png") }, PictureDropPayload.Read(data));
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
}
