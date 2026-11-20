using System.Buffers.Binary;
using System.Text;
using Chisel.Core.Pictures;

namespace Chisel.Core.Tests.Pictures;

// Tiny files built byte by byte: only the headers a real file starts with.
public class ImageSnifferTests
{
    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), (uint)height);
        bytes[24] = 8;
        bytes[25] = 2;
        return bytes;
    }

    private static byte[] Jpeg(int width, int height)
    {
        var list = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        list.AddRange(new byte[14]);
        list.AddRange(new byte[] { 0xFF, 0xC0, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 3 });
        list.AddRange(new byte[10]);
        return list.ToArray();
    }

    private static byte[] Gif(int width, int height, int frames)
    {
        var list = new List<byte>();
        list.AddRange(Encoding.ASCII.GetBytes("GIF89a"));
        list.AddRange(new[] { (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8), (byte)0x00, (byte)0, (byte)0 });
        for (var i = 0; i < frames; i++)
        {
            // Graphic control extension, then an image with no local table and one empty data block.
            list.AddRange(new byte[] { 0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00 });
            list.AddRange(new byte[] { 0x2C, 0, 0, 0, 0, (byte)width, 0, (byte)height, 0, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00 });
        }
        list.Add(0x3B);
        return list.ToArray();
    }

    private static byte[] WebPLossless(int width, int height)
    {
        var bytes = new byte[30];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        "VP8L"u8.CopyTo(bytes.AsSpan(12));
        bytes[20] = 0x2F;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(21), (uint)(width - 1) | ((uint)(height - 1) << 14));
        return bytes;
    }

    private static byte[] WebPExtended(int width, int height, bool animated)
    {
        var bytes = new byte[30];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        "VP8X"u8.CopyTo(bytes.AsSpan(12));
        bytes[20] = animated ? (byte)0x02 : (byte)0;
        bytes[24] = (byte)(width - 1);
        bytes[25] = (byte)((width - 1) >> 8);
        bytes[27] = (byte)(height - 1);
        bytes[28] = (byte)((height - 1) >> 8);
        return bytes;
    }

    private static byte[] WebPLossy(int width, int height)
    {
        var bytes = new byte[30];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        "VP8 "u8.CopyTo(bytes.AsSpan(12));
        bytes[23] = 0x9D;
        bytes[24] = 0x01;
        bytes[25] = 0x2A;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28), (ushort)height);
        return bytes;
    }

    [Fact]
    public void Png_IsRecognisedWithItsSize() =>
        Assert.Equal(new ImageInfo(ImageFormats.Png, 640, 480, false), ImageSniffer.Sniff(Png(640, 480)));

    [Fact]
    public void Jpeg_IsRecognisedWithItsSize() =>
        Assert.Equal(new ImageInfo(ImageFormats.Jpeg, 1920, 1080, false), ImageSniffer.Sniff(Jpeg(1920, 1080)));

    [Fact]
    public void Gif_TellsAStillFromAnAnimation()
    {
        Assert.Equal(new ImageInfo(ImageFormats.Gif, 20, 10, false), ImageSniffer.Sniff(Gif(20, 10, 1)));
        Assert.Equal(new ImageInfo(ImageFormats.Gif, 20, 10, true), ImageSniffer.Sniff(Gif(20, 10, 3)));
    }

    [Fact]
    public void WebP_IsRecognisedInAllThreeLayouts()
    {
        Assert.Equal(new ImageInfo(ImageFormats.WebP, 300, 200, false), ImageSniffer.Sniff(WebPLossless(300, 200)));
        Assert.Equal(new ImageInfo(ImageFormats.WebP, 800, 600, true), ImageSniffer.Sniff(WebPExtended(800, 600, true)));
        Assert.Equal(new ImageInfo(ImageFormats.WebP, 321, 123, false), ImageSniffer.Sniff(WebPLossy(321, 123)));
    }

    [Fact]
    public void ThePicturesNameIsNeverTrusted_OnlyItsFirstBytes()
    {
        // A script or an SVG named like a picture is not a picture.
        Assert.Null(ImageSniffer.Sniff("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>"u8));
        Assert.Null(ImageSniffer.Sniff("<html><img src=x></html>"u8));
        Assert.Null(ImageSniffer.Sniff("MZ\u0090\u0000\u0003"u8));
        // And a real picture is one whatever it is called.
        Assert.NotNull(ImageSniffer.Sniff(Png(2, 2)));
    }

    [Fact]
    public void ShortOrCutOffFiles_AreNotPictures()
    {
        Assert.Null(ImageSniffer.Sniff([]));
        Assert.Null(ImageSniffer.Sniff(Png(5, 5).AsSpan(0, 12)));
        Assert.Null(ImageSniffer.Sniff(Jpeg(5, 5).AsSpan(0, 12)));
        Assert.Null(ImageSniffer.Sniff(WebPLossless(5, 5).AsSpan(0, 20)));
        Assert.Null(ImageSniffer.Sniff(Png(0, 5)));
    }

    [Theory]
    [InlineData(100, 100, null)]
    [InlineData(12000, 8000, null)]
    [InlineData(12001, 10, "larger")]
    [InlineData(10, 12001, "larger")]
    [InlineData(11000, 10000, "megapixels")]
    [InlineData(0, 10, "size")]
    public void Limits_RefuseHugePictures(int width, int height, string? messagePart)
    {
        var message = ImageLimits.Check(width, height);
        if (messagePart is null)
        {
            Assert.Null(message);
        }
        else
        {
            Assert.Contains(messagePart, message);
        }
    }
}
