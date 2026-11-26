using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Chisel.App.Views.Widgets;
using Chisel.Core.Clock;

namespace Chisel.App.Tests.Headless;

// The analog stopwatch face at every size, in every built-in look: it always draws, within its bounds, and a look is drawn the same
// whichever look or size was drawn before it.
public class StopwatchFaceScalingTests
{
    private static readonly double[] Sizes = [30, 60, 90, 110, 130, 160, 200, 260, 400];

    private static StopwatchFace Face(double size, string id) =>
        new() { Width = size, Height = size, Style = ClockPresets.Apply(id, ClockStyle.Default), ElapsedSource = () => 754.3 };

    private static byte[] Draw(StopwatchFace face, double size)
    {
        face.Width = face.Height = size;
        face.Measure(new Size(size, size));
        face.Arrange(new Rect(0, 0, size, size));
        var bitmap = new RenderTargetBitmap(new PixelSize((int)size, (int)size));
        using (var context = bitmap.CreateDrawingContext())
        {
            face.Draw(context);
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    [AvaloniaFact]
    public void TheFaceChoosesItsDetailFromItsOwnDiameter_AtEverySize()
    {
        foreach (var size in Sizes)
        {
            var face = Face(size, "classic");
            Draw(face, size);
            Assert.Equal(StopwatchLayout.For(StopwatchLayout.DiameterFor(size)), face.Metrics);
            Assert.True(face.FaceRadius * 2 <= size - 2 * StopwatchLayout.EdgeGap + 0.001, $"the face is larger than its space at {size}");
        }
    }

    [AvaloniaFact]
    public void EveryLook_AtEverySize_IsDrawnTheSame_WhateverWasDrawnBefore()
    {
        var ids = ClockPresets.All.Select(p => p.Id).ToArray();
        var fresh = new Dictionary<(string, double), byte[]>();
        foreach (var id in ids)
        {
            foreach (var size in Sizes)
            {
                fresh[(id, size)] = Draw(Face(size, id), size);
            }
        }

        // One long-lived face, resized and restyled in turn (as the widget does while it is resized or the look changes).
        var reused = Face(Sizes[0], ids[0]);
        var random = new Random(3);
        for (var i = 0; i < 120; i++)
        {
            var id = ids[random.Next(ids.Length)];
            var size = Sizes[random.Next(Sizes.Length)];
            reused.Style = ClockPresets.Apply(id, ClockStyle.Default);
            Assert.True(fresh[(id, size)].SequenceEqual(Draw(reused, size)), $"{id} at {size} differs after other looks and sizes");
        }
    }

    [AvaloniaFact]
    public void ShrinkingTheDial_ChangesThePictureOnlyWhereTheLevelChanges_NotWithAFlickerBetween()
    {
        // The same size drawn twice (before and after a trip through other sizes) is identical: the cache never goes stale.
        var face = Face(200, "pilot");
        var first = Draw(face, 200);
        foreach (var size in new[] { 150, 100, 70, 40, 70, 100, 150 })
        {
            Draw(face, size);
        }
        Assert.True(first.SequenceEqual(Draw(face, 200)));
    }

    [AvaloniaFact]
    public void ContactSheet_ShrinkingTheDial_WritesAPicture()
    {
        double[] sizes = [60, 90, 110, 130, 160, 200, 260];
        var sheet = new RenderTargetBitmap(new PixelSize((int)sizes.Sum() + 10 * sizes.Length, 3 * 270));
        var rows = new[] { "classic", "pilot", "minimal" };
        using (var context = sheet.CreateDrawingContext())
        {
            for (var row = 0; row < rows.Length; row++)
            {
                var x = 5.0;
                foreach (var size in sizes)
                {
                    var face = Face(size, rows[row]);
                    face.Measure(new Size(size, size));
                    face.Arrange(new Rect(0, 0, size, size));
                    using (context.PushTransform(Matrix.CreateTranslation(x, row * 270 + 5)))
                    {
                        face.Draw(context);
                    }
                    x += size + 10;
                }
            }
        }
        var directory = Path.Combine(UiSnapshot.FindRepoRoot(), "artifacts", "ui");
        Directory.CreateDirectory(directory);
        sheet.Save(Path.Combine(directory, "stopwatch-sizes.png"));
    }
}
