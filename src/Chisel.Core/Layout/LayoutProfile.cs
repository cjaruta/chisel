namespace Chisel.Core.Layout;

// A saved arrangement the user can come back to: one of a few numbered slots (1 to MaximumProfiles), with a name.
public sealed record LayoutProfile(int Slot, string Name, WidgetLayout Layout);

public static class LayoutProfiles
{
    public const int MaximumProfiles = 3;

    public static string DefaultName(int slot) => $"Profile {slot}";

    public static bool IsValidSlot(int slot) => slot is >= 1 and <= MaximumProfiles;
}

public interface ILayoutProfileRepository
{
    // The saved profiles, by slot.
    IReadOnlyList<LayoutProfile> GetAll();

    // Saves into the profile's slot, replacing what was there.
    void Save(LayoutProfile profile);

    void Delete(int slot);
}
