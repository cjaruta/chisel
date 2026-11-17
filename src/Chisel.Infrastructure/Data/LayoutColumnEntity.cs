namespace Chisel.Infrastructure.Data;

// One column of the saved layout, keyed by its column index, with its share of the window's width.
public class LayoutColumnEntity
{
    public int Position { get; set; }
    public double WidthWeight { get; set; } = 1;
}
