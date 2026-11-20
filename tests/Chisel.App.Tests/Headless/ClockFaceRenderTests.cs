using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.Headless;

public class ClockFaceRenderTests
{
    private static ClockFace Face(ClockStyle style, double width = 300, double height = 300)
    {
        var face = new ClockFace { Style = style, HourAngle = 285, MinuteAngle = 180, SecondAngle = 30 };
        face.Measure(new Size(width, height));
        face.Arrange(new Rect(0, 0, width, height));
        return face;
    }

    private static byte[] Pixels(ClockFace face)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize((int)face.Bounds.Width, (int)face.Bounds.Height));
        using (var context = bitmap.CreateDrawingContext())
        {
            face.Draw(context);
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    [AvaloniaFact]
    public void EachPreset_DrawsSomethingDifferent()
    {
        var images = ClockPresets.All.Select(p => Convert.ToBase64String(Pixels(Face(p.Style)))).ToList();
        Assert.Equal(images.Count, images.Distinct().Count());
    }

    [AvaloniaTheory]
    [InlineData(HandStyle.Line)]
    [InlineData(HandStyle.Tapered)]
    [InlineData(HandStyle.Baton)]
    [InlineData(HandStyle.Leaf)]
    public void EveryHandStyle_DiffersFromTheOthers(HandStyle style)
    {
        var mine = Convert.ToBase64String(Pixels(Face(ClockStyle.Default with { Hands = style })));
        foreach (var other in Enum.GetValues<HandStyle>().Where(h => h != style))
        {
            Assert.NotEqual(mine, Convert.ToBase64String(Pixels(Face(ClockStyle.Default with { Hands = other }))));
        }
    }

    [AvaloniaTheory]
    [InlineData(ClockAlignment.Left)]
    [InlineData(ClockAlignment.Center)]
    [InlineData(ClockAlignment.Right)]
    public void Alignment_PlacesTheFaceInTheWidget(ClockAlignment alignment)
    {
        var face = Face(ClockStyle.Default with { Alignment = alignment }, 600, 300);
        var expected = alignment switch
        {
            ClockAlignment.Left => face.FaceRadius + ClockFace.EdgeGap,
            ClockAlignment.Right => 600 - face.FaceRadius - ClockFace.EdgeGap,
            _ => 300,
        };
        Assert.Equal(expected, face.FaceCenter.X, 0.5);
    }

    [AvaloniaFact]
    public void WithoutASecondsHand_TheSecondAngleDoesNotChangeThePicture()
    {
        var style = ClockStyle.Default with { ShowSeconds = false };
        var a = Pixels(Face(style));
        var face = Face(style);
        face.SecondAngle = 200;
        Assert.Equal(a, Pixels(face));
    }

    // Once the face is cached (static dial bitmap and hand shapes), drawing a frame must not allocate.
    [AvaloniaFact]
    public void Draw_AllocatesNothingInSteadyState()
    {
        const int frames = 60;
        var offenders = new List<string>();
        foreach (var preset in ClockPresets.All)
        {
            var face = Face(preset.Style);
            var bitmap = new RenderTargetBitmap(new PixelSize(300, 300));
            using var context = bitmap.CreateDrawingContext();
            for (var i = 0; i < 12; i++)
            {
                face.Draw(context);
            }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < frames; i++)
            {
                face.Draw(context);
            }
            var perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / frames;
            if (perFrame > 64)
            {
                offenders.Add($"{preset.Id}: {perFrame} B/frame");
            }
        }
        Assert.True(offenders.Count == 0, string.Join("; ", offenders));
    }
}
