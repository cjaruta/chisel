using System.Diagnostics;

namespace Chisel.Core.Weather;

public enum MotionKind { None, SunGlow, Stars, Clouds, Rain, HeavyRain, Snow, Fog }

// Particle color as 0xRRGGBB (a tint of the scene's gradient) and its largest opacity. Together with the gradient they
// keep the widget's text at 4.5:1 contrast even with two particles stacked (see WeatherBackdropTests).
public readonly record struct MotionStyle(MotionKind Kind, uint Color, double MaxOpacity);

// X, Y: where it is (a cloud, glow or flake: its center; a streak or ray: its start). Size: radius (flake, star, glow),
// half-width (cloud, fog band) or length (streak, ray). Depth: 0 far, 1 middle, 2 near (for stars 1 means "glows",
// for the sun 0 is the glow and 1 a ray). Angle: direction of a streak or ray, in radians from straight down.
public readonly record struct Particle(double X, double Y, double Size, double Opacity, int Depth = 0, double Angle = 0);

// Ambient weather motion: all logic in this file. A particle's place is a pure function of (kind, index, seconds, area),
// and every scene repeats exactly every Loop(kind) seconds, so wraparound is seamless. Turn it all off with MotionEnabled.
public static class WeatherMotion
{
    public const bool MotionEnabled = true;
    public const int MaxParticles = 40;
    private const double ReferenceArea = 150_000;

    // One monotonic clock for the whole app, so a rebuilt control carries on instead of restarting.
    private static readonly long Origin = Stopwatch.GetTimestamp();

    public static double Now => Stopwatch.GetElapsedTime(Origin).TotalSeconds;

    // The particle color is the gradient's top stop pulled toward a light highlight for the scene (blue-white rain,
    // warm sun, pale snow), so it always belongs to its backdrop.
    public static MotionStyle Style(string key)
    {
        var top = WeatherBackdrop.Gradients[key].Top;
        var day = key.EndsWith("-day", StringComparison.Ordinal);
        if (key.StartsWith("clear", StringComparison.Ordinal))
        {
            return day ? new(MotionKind.SunGlow, Tint(top, 0xFFD79A), 0.08) : new(MotionKind.Stars, Tint(top, 0xFFFFFF), 0.17);
        }
        if (key.StartsWith("partly-cloudy", StringComparison.Ordinal) || key.StartsWith("cloudy", StringComparison.Ordinal))
        {
            return new(MotionKind.Clouds, Tint(top, 0xE8EEF8), day ? 0.07 : 0.18);
        }
        if (key.StartsWith("rain", StringComparison.Ordinal))
        {
            return new(MotionKind.Rain, Tint(top, 0xE4EEFF), day ? 0.09 : 0.18);
        }
        if (key.StartsWith("storm", StringComparison.Ordinal))
        {
            return new(MotionKind.HeavyRain, Tint(top, 0xDCE6FA), day ? 0.12 : 0.21);
        }
        if (key.StartsWith("snow", StringComparison.Ordinal))
        {
            return new(MotionKind.Snow, Tint(top, 0xF4F8FF), day ? 0.06 : 0.16);
        }
        return new(MotionKind.Fog, Tint(top, 0xEEF2F6), day ? 0.07 : 0.17);
    }

    private static uint Tint(uint color, uint highlight)
    {
        uint Mix(int shift) => (uint)Math.Round(((color >> shift) & 255) * 0.3 + ((highlight >> shift) & 255) * 0.7);
        return (Mix(16) << 16) | (Mix(8) << 8) | Mix(0);
    }

    // Redraws per second: faster for falling scenes, slow for the rest.
    public static double Fps(MotionKind kind) => kind is MotionKind.Rain or MotionKind.HeavyRain or MotionKind.Snow ? 12 : 8;

    // Seconds after which the scene repeats exactly.
    public static double Loop(MotionKind kind) => kind switch
    {
        MotionKind.Rain => 4,
        MotionKind.HeavyRain => 3,
        MotionKind.Snow => 24,
        MotionKind.Stars => 12,
        MotionKind.SunGlow => 240,
        MotionKind.Clouds => 120,
        MotionKind.Fog => 120,
        _ => 1,
    };

    private static (int Min, int Max) Range(MotionKind kind) => kind switch
    {
        MotionKind.Rain => (6, 18),
        MotionKind.HeavyRain => (9, 27),
        MotionKind.Snow => (6, 18),
        MotionKind.Stars => (4, 10),
        MotionKind.Clouds => (2, 4),
        MotionKind.Fog => (2, 3),
        MotionKind.SunGlow => (7, 7),
        _ => (0, 0),
    };

    // Grows with area up to the cap, so a small widget stays calm and a large one is never busy.
    public static int Count(MotionKind kind, double width, double height)
    {
        var (min, max) = Range(kind);
        return Math.Clamp((int)Math.Ceiling(max * width * height / ReferenceArea), min, max);
    }

    // Cloud blobs are 3 to 5 overlapping circles; Puff gives circle k as offsets and radius in units of the cloud's half-width.
    public static int PuffCount(int index) => 3 + (int)(Unit(MotionKind.Clouds, index, 7) * 3);

    public static (double Dx, double Dy, double Radius) Puff(int index, int puff)
    {
        var n = PuffCount(index);
        var across = n == 1 ? 0 : puff / (double)(n - 1) * 2 - 1;
        var middle = 1 - Math.Abs(across);
        var jitter = Unit(MotionKind.Clouds, index * 8 + puff, 8) - 0.5;
        return (across * 0.5 + jitter * 0.1, -0.1 * middle + jitter * 0.06, 0.4 + 0.25 * middle);
    }

    public static Particle Get(MotionKind kind, int index, double seconds, double width, double height)
    {
        var phase = seconds / Loop(kind);
        var x0 = Unit(kind, index, 1);
        var y0 = Unit(kind, index, 2);
        var size = Unit(kind, index, 3);
        var opacity = Unit(kind, index, 4);
        var cycles = 1 + (int)(Unit(kind, index, 5) * 3);
        var depth = index % 3;

        switch (kind)
        {
            case MotionKind.Rain:
            case MotionKind.HeavyRain:
            {
                var heavy = kind == MotionKind.HeavyRain;
                var length = (heavy ? 1.3 : 1.0) * (depth switch { 0 => 8, 1 => 14, _ => 22 }) * (0.85 + 0.3 * size);
                var slant = heavy ? 0.26 : 0.18;
                var y = Frac(y0 + (depth + (heavy ? 3 : 2)) * phase) * (height + 2 * length) - length;
                var run = slant * (height + 2 * length);
                var x = -run + x0 * (width + run) + slant * (y + length);
                return new Particle(x, y, length, (0.3 + 0.35 * depth) * (0.8 + 0.2 * opacity), depth, Math.Atan(slant));
            }
            case MotionKind.Snow:
            {
                var radius = (0.9 + 0.7 * depth + 0.15 * depth * depth) * (0.8 + 0.4 * size);
                var sway = Math.Sin(2 * Math.PI * ((depth + 2) * phase + y0)) * (3 + 4 * depth) * Math.Min(1, width / 500);
                var y = Frac(y0 + (depth + 1) * phase) * (height + 2 * radius) - radius;
                return new Particle((0.03 + 0.94 * x0) * width + sway, y, radius, (0.35 + 0.28 * depth) * (0.8 + 0.2 * opacity), depth);
            }
            case MotionKind.Stars:
            {
                var glows = Unit(kind, index, 6) < 0.3;
                var twinkle = 0.5 + 0.5 * Math.Sin(2 * Math.PI * (cycles * phase + x0));
                return new Particle(x0 * width, y0 * height * 0.85, 0.8 + 2.0 * size * size, (glows ? 0.35 : 0.2) + (glows ? 0.65 : 0.8) * twinkle, glows ? 1 : 0);
            }
            case MotionKind.SunGlow:
            {
                var cx = width * 0.78;
                var cy = height * 0.28;
                var breathe = 0.5 + 0.5 * Math.Sin(2 * Math.PI * 15 * phase);
                var reach = Math.Min(width, height);
                if (index == 0)
                {
                    return new Particle(cx, cy, reach * (0.72 + 0.1 * breathe), 0.7 + 0.3 * breathe);
                }
                // Six faint rays, turning once per loop (very slowly).
                var angle = 2 * Math.PI * ((index - 1) / 6.0 + phase);
                var inner = reach * 0.22;
                return new Particle(cx + Math.Sin(angle) * inner, cy + Math.Cos(angle) * inner, reach * 0.85,
                    0.5 + 0.4 * Unit(kind, index, 4), 1, angle);
            }
            case MotionKind.Clouds:
            {
                var near = index % 2;
                var half = Math.Min(width * 0.22, 110) * (near == 1 ? 1.0 : 0.62) * (0.85 + 0.3 * size);
                var bob = Math.Sin(2 * Math.PI * (3 * phase + x0)) * (2 + 3 * near);
                return new Particle(Frac(x0 + (1 + near) * phase) * (width + 4 * half) - 2 * half, (0.2 + 0.5 * y0) * height + bob, half,
                    (near == 1 ? 0.8 : 0.5) * (0.8 + 0.2 * opacity), near);
            }
            case MotionKind.Fog:
            {
                var half = width * (0.5 + 0.2 * size);
                var lap = 1 + index % 2;
                return new Particle(Frac(x0 + lap * phase) * (width + 4 * half) - 2 * half,
                    (0.25 + 0.5 * y0) * height + Math.Sin(2 * Math.PI * (2 * phase + x0)) * 6, half, 0.55 + 0.45 * opacity, lap - 1);
            }
            default:
                return default;
        }
    }

    private static double Frac(double value) => value - Math.Floor(value);

    // A stable 0..1 number for (kind, index, salt): the same everywhere, every run.
    private static double Unit(MotionKind kind, int index, int salt)
    {
        var h = unchecked((uint)((int)kind * 73856093 ^ index * 19349663 ^ salt * 83492791));
        h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15; h *= 0x846ca68b; h ^= h >> 16;
        return h / 4294967296.0;
    }
}
