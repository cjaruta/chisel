namespace Chisel.Core.Layout;

public interface IWidgetLayoutRepository
{
    // Null when no layout has been saved yet.
    WidgetLayout? Get();
    void Save(WidgetLayout layout);
}
