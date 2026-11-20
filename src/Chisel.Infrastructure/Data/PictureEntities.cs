namespace Chisel.Infrastructure.Data;

// One stored picture file (the file itself is in the pictures folder, named by its hash).
public class PictureAssetEntity
{
    public Guid Id { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public long Bytes { get; set; }
    public string? SourceUrl { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}

// One picture in a frame's list, with its crop. FrameId is the frame's widget instance id.
public class FramePictureEntity
{
    public Guid Id { get; set; }
    public string FrameId { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public int Position { get; set; }
    public double FocalX { get; set; } = 0.5;
    public double FocalY { get; set; } = 0.5;
    public double Zoom { get; set; } = 1;
}

// A frame's options. One row per frame instance.
public class FrameOptionsEntity
{
    public string FrameId { get; set; } = string.Empty;
    public int Fit { get; set; }
    public bool BlurFill { get; set; }
    public int Mode { get; set; }
    public int IntervalSeconds { get; set; } = 30;
    public int Order { get; set; }
    public int Transition { get; set; }
    public int CurrentIndex { get; set; }
}
