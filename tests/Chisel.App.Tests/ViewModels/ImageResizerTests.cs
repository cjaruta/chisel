using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Chisel.App.Services;
using Chisel.App.Tests.Support;
using Chisel.Core.Pictures;

namespace Chisel.App.Tests.ViewModels;

// The display-size copy "Keep original files" off stores: a photo becomes a smaller JPEG, a picture with see-through
// parts stays a PNG so the transparency survives.
public class ImageResizerTests
{
    private static byte[] Transparent(int width, int height)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        using (var context = bitmap.CreateDrawingContext())
        {
            // Half the picture is drawn, the rest is left see-through.
            context.DrawRectangle(Brushes.OrangeRed, null, new Rect(0, 0, width / 2.0, height));
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    [AvaloniaFact]
    public void AnOpaquePicture_IsStoredAsASmallerJpeg_AtTheRequestedSize()
    {
        var original = SampleImages.Landscape(1600, 1200);

        var copy = new AvaloniaImageResizer().Resize(original, 800, 600)!;

        var info = ImageSniffer.Sniff(copy)!;
        Assert.Equal(ImageFormats.Jpeg, info.MimeType);
        Assert.Equal((800, 600), (info.Width, info.Height));
        Assert.True(copy.Length < original.Length, $"{copy.Length} should be smaller than {original.Length}");
    }

    [AvaloniaFact]
    public void APictureWithTransparency_StaysAPng()
    {
        var copy = new AvaloniaImageResizer().Resize(Transparent(400, 300), 200, 150)!;

        var info = ImageSniffer.Sniff(copy)!;
        Assert.Equal(ImageFormats.Png, info.MimeType);
        Assert.Equal((200, 150), (info.Width, info.Height));
    }

    [AvaloniaFact]
    public void ABrokenFile_GivesNothing() =>
        Assert.Null(new AvaloniaImageResizer().Resize([1, 2, 3], 10, 10));
}
