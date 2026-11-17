namespace Chisel.Core.Layout;

// How many columns a layout can have, and how narrow a column may get before no more are offered.
public static class LayoutLimits
{
    public const int MaximumColumns = 4;

    // In pixels. A new column is only offered when every column can still be at least this wide.
    public const double MinimumColumnWidth = 260;
}
