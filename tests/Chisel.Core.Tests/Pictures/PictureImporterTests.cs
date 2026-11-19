using System.Buffers.Binary;
using Chisel.Core.Pictures;
using Microsoft.Extensions.Time.Testing;

namespace Chisel.Core.Tests.Pictures;

public class PictureImporterTests
{
    private sealed class FakeResizer : IImageResizer
    {
        public (int Width, int Height)? Asked { get; private set; }

        public byte[]? Resize(byte[] data, int width, int height)
        {
            Asked = (width, height);
            return Png(width, height);
        }
    }

    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), (uint)height);
        return bytes;
    }

    private static byte[] AnimatedGif(int width, int height)
    {
        var list = new List<byte> { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8), 0, 0, 0 };
        for (var i = 0; i < 2; i++)
        {
            list.AddRange(new byte[] { 0x2C, 0, 0, 0, 0, (byte)width, 0, (byte)height, 0, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00 });
        }
        list.Add(0x3B);
        return list.ToArray();
    }

    private readonly FakeRepository _repository = new();
    private readonly FakeStore _store = new();
    private readonly FakeResizer _resizer = new();

    private PictureImporter Importer() => new(_repository, _store, _resizer, new FakeTimeProvider());

    [Fact]
    public void ATooBigOrWrongFile_IsRefusedWithAShortMessage_AndNothingIsStored()
    {
        var importer = Importer();

        Assert.Contains("PNG, JPEG, GIF or WebP", importer.Prepare("<svg/>"u8.ToArray(), "x.png", null, true).Error);
        Assert.Contains("12000", importer.Prepare(Png(12001, 10), "x.png", null, true).Error);
        Assert.Contains("megapixels", importer.Prepare(Png(11000, 10000), "x.png", null, true).Error);
        Assert.Contains("30 MB", importer.Prepare(new byte[31 * 1024 * 1024], "x.png", null, true).Error);
        Assert.Empty(_repository.Stored);
        Assert.Equal(0, _store.Writes);
    }

    [Fact]
    public void AGoodPicture_IsStoredByItsHash_WithItsSizeAndSource()
    {
        var importer = Importer();
        var prepared = importer.Prepare(Png(40, 30), "holiday.png", "https://example.com/holiday.png", true).Picture!;

        var asset = importer.Commit(prepared);

        Assert.Equal((40, 30), (asset.Width, asset.Height));
        Assert.Equal("holiday.png", asset.FileName);
        Assert.Equal("https://example.com/holiday.png", asset.SourceUrl);
        Assert.Equal(ImageFormats.Png, asset.MimeType);
        Assert.Equal(64, asset.Hash.Length);
        Assert.True(_store.Exists(asset.StoredName));
        Assert.Equal(prepared.Bytes.Length, asset.Bytes);
        Assert.Same(asset, Assert.Single(_repository.Stored));
    }

    [Fact]
    public void TheSameBytesTwice_AreStoredOnce()
    {
        var importer = Importer();
        var first = importer.Commit(importer.Prepare(Png(8, 8), "a.png", null, true).Picture!);
        var second = importer.Commit(importer.Prepare(Png(8, 8), "b.png", "https://example.com/b.png", true).Picture!);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(_repository.Stored);
        Assert.Equal(1, _store.Writes);
    }

    [Fact]
    public void ARowWhoseFileWasLost_IsWrittenAgain()
    {
        var importer = Importer();
        var asset = importer.Commit(importer.Prepare(Png(8, 8), "a.png", null, true).Picture!);
        _store.Delete(asset.StoredName);

        importer.Commit(importer.Prepare(Png(8, 8), "a.png", null, true).Picture!);

        Assert.True(_store.Exists(asset.StoredName));
        Assert.Single(_repository.Stored);
    }

    [Fact]
    public void WithKeepOriginalsOff_ABigStillPicture_IsStoredAsADisplaySizeCopy()
    {
        var importer = Importer();
        var prepared = importer.Prepare(Png(6000, 4000), "big.png", null, keepOriginals: false).Picture!;

        Assert.Equal((2048, 1365), _resizer.Asked);
        Assert.Equal((2048, 1365), (prepared.Info.Width, prepared.Info.Height));
        Assert.Equal(ImageFormats.Png, prepared.Info.MimeType);
    }

    [Fact]
    public void WithKeepOriginalsOff_SmallPicturesAndAnimatedGifs_AreKeptAsTheyCame()
    {
        var importer = Importer();
        var small = Png(800, 600);
        Assert.Equal(small, importer.Prepare(small, "s.png", null, false).Picture!.Bytes);

        var gif = AnimatedGif(3000, 200);
        var prepared = importer.Prepare(gif, "anim.gif", null, false).Picture!;
        Assert.Equal(gif, prepared.Bytes);
        Assert.True(prepared.Info.IsAnimated);
        Assert.Null(_resizer.Asked);
    }
}
