namespace Chisel.Core.Media;

public enum VolumeIcon { Muted, Low, Medium, High }

public static class VolumeMath
{
    public static double Clamp(double level) => double.IsNaN(level) ? 0 : Math.Clamp(level, 0, 1);

    // Muted (or silent) shows the muted icon; otherwise thirds of the range.
    public static VolumeIcon Icon(double level, bool muted)
    {
        level = Clamp(level);
        return muted || level <= 0 ? VolumeIcon.Muted : level < 1d / 3 ? VolumeIcon.Low : level < 2d / 3 ? VolumeIcon.Medium : VolumeIcon.High;
    }

    public static string Percent(double level) => $"{(int)Math.Round(Clamp(level) * 100)}%";
}
