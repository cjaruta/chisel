namespace Chisel.Infrastructure.Data;

// The user's saved clock faces: one row (Id is always 1) holding the versioned JSON document (see Chisel.Core ClockFaceJson).
// Kept as one document so a face can gain options without a new column each time.
public class ClockFaceLibraryEntity
{
    public int Id { get; set; } = 1;
    public string Json { get; set; } = string.Empty;
}
