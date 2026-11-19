using System.Threading;

namespace Chisel.App.Services;

// A small RGBA copy of a cover (rows top to bottom, 4 bytes per pixel, not premultiplied).
public readonly record struct ArtPixels(byte[] Rgba, int Width, int Height);

// Decodes cover bytes and shrinks them to about 32 pixels wide. Called on a background thread; null means it could not be decoded.
public interface IArtSampler
{
    ArtPixels? Sample(byte[] art, CancellationToken cancel);
}
