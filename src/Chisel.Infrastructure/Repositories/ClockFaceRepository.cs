using Chisel.Core.Clock;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class ClockFaceRepository : IClockFaceStore
{
    private readonly ChiselDbContext _context;

    public ClockFaceRepository(ChiselDbContext context)
    {
        _context = context;
    }

    // Missing, empty or damaged text is no faces (nothing is thrown); the next Save replaces it.
    public IReadOnlyList<CustomClockFace> Load() =>
        ClockFaceJson.Parse(_context.ClockFaceLibraries.FirstOrDefault()?.Json);

    public void Save(IReadOnlyList<CustomClockFace> faces)
    {
        var json = ClockFaceJson.Serialize(faces);
        var row = _context.ClockFaceLibraries.FirstOrDefault();
        if (row is null)
        {
            _context.ClockFaceLibraries.Add(new ClockFaceLibraryEntity { Json = json });
        }
        else
        {
            row.Json = json;
        }
        _context.SaveChanges();
    }
}
