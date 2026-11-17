namespace Chisel.Infrastructure.Data;

// A widget the user took off the dashboard. Remembering it keeps it hidden on the next start,
// unlike a widget the saved layout has never had, which gets placed.
public class HiddenWidgetEntity
{
    public string WidgetId { get; set; } = string.Empty;
    public int Position { get; set; }
}
