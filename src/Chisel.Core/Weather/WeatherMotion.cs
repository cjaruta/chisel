using System.Diagnostics;

namespace Chisel.Core.Weather;

public enum MotionKind { None, SunGlow, Stars, Clouds, Rain, HeavyRain, Snow, Fog }

// Particle color as 0xRRGGBB and its largest opacity. Colors stay at or under the luminance the widget's dim text
// allows (see WeatherBackdropTests), so particles are subtle: tints and shadows, not bright shapes.
public readonly record struct MotionStyle(MotionKind Kind, uint Color, double MaxOpacity);

// X, Y: center. Size: radius (glow, star, flake), half-width (cloud, fog) or streak length (rain).
public readonly record struct Particle(double X, double Y, double Size, double Opacity);

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

    public static MotionStyle Style(string key)
    {
        var day = key.EndsWith("-day", StringComparison.Ordinal);
        if (key.StartsWith("clear", StringComparison.Ordinal))
        {
            return day ? new(MotionKind.SunGlow, 0x2F2413, 0.9) : new(MotionKind.Stars, 0x1A2438, 1.0);
        }
        if (key.StartsWith("partly-cloudy", StringComparison.Ordinal) || key.StartsWith("cloudy", StringComparison.Ordinal))
        {
            return day ? new(MotionKind.Clouds, 0x0A1018, 0.5) : new(MotionKind.Clouds, 0x1E2430, 0.8);
        }
        if (key.StartsWith("rain", StringComparison.Ordinal))
        {
            return day ? new(MotionKind.Rain, 0x08101A, 0.6) : new(MotionKind.Rain, 0x1A2431, 0.9);
        }
        if (key.StartsWith("storm", StringComparison.Ordinal))
        {
            return day ? new(MotionKind.HeavyRain, 0x080C14, 0.7) : new(MotionKind.HeavyRain, 0x1A2230, 0.9);
        }
        if (key.StartsWith("snow", StringComparison.Ordinal))
        {
            return day ? new(MotionKind.Snow, 0x1C2328, 0.9) : new(MotionKind.Snow, 0x1E252C, 0.9);
        }
        return day ? new(MotionKind.Fog, 0x0A1214, 0.45) : new(MotionKind.Fog, 0x1E2326, 0.6);
    }

    // Redraws per second: faster for falling scenes, slow for the rest.
    public static double Fps(MotionKind kind) => kind is MotionKind.Rain or MotionKind.HeavyRain or MotionKind.Snow ? 12 : 8;

    // Seconds after which the scene repeats exactly.
    public static double Loop(MotionKind kind) => kind switch
    {
        MotionKind.Rain => 2,
        MotionKind.HeavyRain => 1.5,
        MotionKind.Snow => 24,
        MotionKind.Stars => 12,
        MotionKind.SunGlow => 16,
        MotionKind.Clouds => 90,
        MotionKind.Fog => 60,
        _ => 1,
    };

    private static (int Min, int Max) Range(MotionKind kind) => kind switch
    {
        MotionKind.Rain => (6, 16),
        MotionKind.HeavyRain => (10, 28),
        MotionKind.Snow => (6, 18),
        MotionKind.Stars => (4, 10),
        MotionKind.Clouds => (2, 3),
        MotionKind.Fog => (2, 3),
        MotionKind.SunGlow => (1, 1),
        _ => (0, 0),
    };

    // Grows with area up to the cap, so a small widget stays calm and a large one is never busy.
    public static int Count(MotionKind kind, double width, double height)
    {
        var (min, max) = Range(kind);
        return Math.Clamp((int)Math.Ceiling(max * width * height / ReferenceArea), min, max);
    }

    public static Particle Get(MotionKind kind, int index, double seconds, double width, double height)
    {
        var phase = seconds / Loop(kind);
        var x0 = Unit(kind, index, 1);
        var y0 = Unit(kind, index, 2);
        var size = Unit(kind, index, 3);
        var opacity = Unit(kind, index, 4);
        var laps = 1 + (int)(Unit(kind, index, 5) * 3);

        switch (kind)
        {
            case MotionKind.Rain:
            case MotionKind.HeavyRain:
            {
                var heavy = kind == MotionKind.HeavyRain;
                return new Particle(x0 * width, Frac(y0 + (laps + 2) * phase) * height,
                    (heavy ? 14 : 10) + size * 8, 0.4 + 0.6 * opacity);
            }
            case MotionKind.Snow:
            {
                var sway = Math.Sin(2 * Math.PI * (laps * phase + y0)) * Math.Min(14, width * 0.03) * (0.4 + 0.6 * size);
                return new Particle((0.03 + 0.94 * x0) * width + sway, Frac(y0 + laps * phase) * height, 1.2 + 1.4 * size, 0.5 + 0.5 * opacity);
            }
            case MotionKind.Stars:
            {
                var twinkle = 0.5 + 0.5 * Math.Sin(2 * Math.PI * (laps * phase + x0));
                return new Particle(x0 * width, y0 * height * 0.8, 0.8 + 0.8 * size, 0.25 + 0.75 * twinkle);
            }
            case MotionKind.SunGlow:
            {
                var breathe = 0.5 + 0.5 * Math.Sin(2 * Math.PI * phase);
                return new Particle(width * 0.78, height * 0.28, Math.Min(width, height) * (0.55 + 0.1 * breathe), 0.6 + 0.4 * breathe);
            }
            case MotionKind.Clouds:
            {
                var half = Math.Min(width * 0.25, 120) * (0.7 + 0.5 * size);
                return new Particle(Frac(x0 + phase) * (width + 4 * half) - 2 * half, (0.15 + 0.6 * y0) * height, half, 0.5 + 0.5 * opacity);
            }
            case MotionKind.Fog:
            {
                var half = width * (0.45 + 0.2 * size);
                return new Particle(Frac(x0 + phase) * (width + 4 * half) - 2 * half, (0.2 + 0.6 * y0) * height, half, 0.5 + 0.5 * opacity);
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
