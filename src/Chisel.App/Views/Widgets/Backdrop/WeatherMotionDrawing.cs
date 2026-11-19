using System;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Chisel.Core.Weather;

namespace Chisel.App.Views.Widgets.Backdrop;

// The one place a motion scene is drawn. The render-thread handler and the offscreen render (tests, motion sheets)
// both call Draw, so what is judged offline is what runs. Brushes and pens are built once per scene key and shared;
// drawing a frame only reads positions and issues draw calls, with no allocation.
public static class WeatherMotionDrawing
{
    // Opacity is baked into a few pre-built brushes (steps up to the scene's MaxOpacity) instead of pushing opacity per particle.
    private const int Levels = 4;

    public sealed class Scene
    {
        public MotionStyle Style { get; }
        internal readonly IImmutableBrush[] Solid = new IImmutableBrush[Levels];
        internal readonly IImmutableBrush[] Soft = new IImmutableBrush[Levels];
        // Pens by depth then opacity level: Pen[depth * Levels + level].
        internal readonly ImmutablePen[] Pen = new ImmutablePen[3 * Levels];

        internal Scene(MotionStyle style)
        {
            Style = style;
            for (var i = 0; i < Levels; i++)
            {
                var alpha = (byte)Math.Round(255 * style.MaxOpacity * (i + 1) / Levels);
                var color = Color.FromArgb(alpha, (byte)(style.Color >> 16), (byte)(style.Color >> 8), (byte)style.Color);
                Solid[i] = new ImmutableSolidColorBrush(color);
                Soft[i] = new ImmutableRadialGradientBrush(
                    new[] { new ImmutableGradientStop(0, color), new ImmutableGradientStop(0.5, Color.FromArgb((byte)(alpha * 0.6), color.R, color.G, color.B)), new ImmutableGradientStop(1, Color.FromArgb(0, color.R, color.G, color.B)) }, radius: 0.5);
                for (var depth = 0; depth < 3; depth++)
                {
                    Pen[depth * Levels + i] = new ImmutablePen(new ImmutableSolidColorBrush(color), StrokeWidth(style.Kind, depth), lineCap: PenLineCap.Round);
                }
            }
        }
    }

    // Far streaks are thinner than near ones; sun rays are wide and soft.
    private static double StrokeWidth(MotionKind kind, int depth) => kind switch
    {
        MotionKind.Rain => 0.8 + 0.45 * depth,
        MotionKind.HeavyRain => 0.9 + 0.55 * depth,
        MotionKind.SunGlow => 5 + 9 * (2 - depth),
        _ => 1,
    };

    private static readonly ConcurrentDictionary<string, Scene> Scenes = new();

    public static Scene SceneFor(string key) => Scenes.GetOrAdd(key, static k => new Scene(WeatherMotion.Style(k)));

    // The two drawing APIs (high-level DrawingContext, and the render thread's ImmediateDrawingContext) share no base type,
    // so the scene is drawn through this small surface; both adapters are structs, so nothing is allocated per frame.
    internal interface ISurface
    {
        void Line(ImmutablePen pen, Point from, Point to);

        void Ellipse(IImmutableBrush brush, Point center, double radiusX, double radiusY);
    }

    private readonly struct ContextSurface(DrawingContext context) : ISurface
    {
        public void Line(ImmutablePen pen, Point from, Point to) => context.DrawLine(pen, from, to);

        public void Ellipse(IImmutableBrush brush, Point center, double radiusX, double radiusY) =>
            context.DrawEllipse(brush, null, center, radiusX, radiusY);
    }

    internal readonly struct ImmediateSurface(ImmediateDrawingContext context) : ISurface
    {
        public void Line(ImmutablePen pen, Point from, Point to) => context.DrawLine(pen, from, to);

        public void Ellipse(IImmutableBrush brush, Point center, double radiusX, double radiusY) =>
            context.DrawEllipse(brush, null, center, radiusX, radiusY);
    }

    // Offscreen: draws the scene at (0,0) in a surface of the given size, clipped to it.
    public static void Draw(DrawingContext context, string key, Size size, double seconds)
    {
        using var clip = context.PushClip(new Rect(size));
        Draw(new ContextSurface(context), SceneFor(key), size, seconds);
    }

    internal static void Draw<TSurface>(TSurface context, Scene scene, Size size, double seconds) where TSurface : ISurface
    {
        if (size.Width < 1 || size.Height < 1)
        {
            return;
        }
        var kind = scene.Style.Kind;
        var count = WeatherMotion.Count(kind, size.Width, size.Height);
        for (var i = 0; i < count; i++)
        {
            var p = WeatherMotion.Get(kind, i, seconds, size.Width, size.Height);
            var level = Math.Min(Levels - 1, (int)(p.Opacity * Levels));
            var center = new Point(p.X, p.Y);
            switch (kind)
            {
                case MotionKind.Rain:
                case MotionKind.HeavyRain:
                    context.Line(scene.Pen[p.Depth * Levels + level], center,
                        new Point(p.X + Math.Sin(p.Angle) * p.Size, p.Y + Math.Cos(p.Angle) * p.Size));
                    break;
                case MotionKind.Snow:
                    context.Ellipse(scene.Solid[level], center, p.Size, p.Size);
                    break;
                case MotionKind.Stars:
                    context.Ellipse(scene.Solid[level], center, p.Size, p.Size);
                    if (p.Depth == 1)
                    {
                        context.Ellipse(scene.Soft[Math.Max(0, level - 1)], center, p.Size * 6, p.Size * 6);
                    }
                    break;
                case MotionKind.Clouds:
                    for (var k = 0; k < WeatherMotion.PuffCount(i); k++)
                    {
                        var (dx, dy, radius) = WeatherMotion.Puff(i, k);
                        context.Ellipse(scene.Soft[level], new Point(p.X + dx * p.Size, p.Y + dy * p.Size), radius * p.Size, radius * p.Size);
                    }
                    break;
                case MotionKind.Fog:
                    context.Ellipse(scene.Soft[level], center, p.Size, p.Size * 0.14);
                    break;
                case MotionKind.SunGlow:
                    if (p.Depth == 0)
                    {
                        context.Ellipse(scene.Soft[level], center, p.Size, p.Size);
                        context.Ellipse(scene.Soft[level], center, p.Size * 0.5, p.Size * 0.5);
                    }
                    else
                    {
                        // A ray is three stacked strokes (wide and faint to narrow), so its edges are soft.
                        var end = new Point(p.X + Math.Sin(p.Angle) * p.Size, p.Y + Math.Cos(p.Angle) * p.Size);
                        context.Line(scene.Pen[0 * Levels + Math.Max(0, level - 2)], center, end);
                        context.Line(scene.Pen[1 * Levels + Math.Max(0, level - 2)], center, end);
                        context.Line(scene.Pen[2 * Levels + Math.Max(0, level - 2)], center, end);
                    }
                    break;
            }
        }
    }
}
