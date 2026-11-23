using System.Collections.Generic;
using Chisel.Core.Clock;

namespace Chisel.App.Services;

// Saved clock faces kept only in memory, as the same JSON text the database holds: what the main view model uses when no real
// store is given (tests, previews). SetRawText puts any text there, to see how damaged data is handled.
public sealed class MemoryClockFaceStore : IClockFaceStore
{
    private string? _json;

    public IReadOnlyList<CustomClockFace> Load() => ClockFaceJson.Parse(_json);

    public void Save(IReadOnlyList<CustomClockFace> faces) => _json = ClockFaceJson.Serialize(faces);

    public void SetRawText(string? text) => _json = text;
}
