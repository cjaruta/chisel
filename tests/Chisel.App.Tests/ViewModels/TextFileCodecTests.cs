using System.Text;
using Chisel.App.Services;
using Xunit;

namespace Chisel.App.Tests.ViewModels;

public class TextFileCodecTests
{
    [Fact]
    public void RoundTrip_KeepsBlankLinesIndentationNewlinesAndUnicode()
    {
        var text = "one\r\n\r\n\ttab\n    spaces\r\nünï ✓ 日本語 😀\n";

        var back = TextFileCodec.Decode(new MemoryStream(TextFileCodec.Encode(text)));

        Assert.Equal(text, back);
    }

    [Fact]
    public void Encode_WritesUtf8WithoutAByteOrderMark()
    {
        var bytes = TextFileCodec.Encode("é");

        Assert.Equal(new byte[] { 0xC3, 0xA9 }, bytes);
    }

    [Fact]
    public void Decode_ReadsFilesWithAByteOrderMark_AndStripsIt()
    {
        var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("héllo")).ToArray();
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("héllo")).ToArray();

        Assert.Equal("héllo", TextFileCodec.Decode(new MemoryStream(utf8Bom)));
        Assert.Equal("héllo", TextFileCodec.Decode(new MemoryStream(utf16)));
    }

    [Fact]
    public void Decode_RejectsBytesThatAreNotText_InsteadOfMangling()
    {
        Assert.Throws<InvalidDataException>(() => TextFileCodec.Decode(new MemoryStream(new byte[] { 0x68, 0xFF, 0xFE, 0xFD, 0x69 })));
    }

    [Fact]
    public void Decode_EmptyFile_IsAnEmptyString()
    {
        Assert.Equal("", TextFileCodec.Decode(new MemoryStream()));
    }
}
