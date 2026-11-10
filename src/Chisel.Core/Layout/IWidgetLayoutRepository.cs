namespace Chisel.Core.Layout;

public interface IWidgetLayoutRepository
{
    WidgetLayout Get();
    void Save(WidgetLayout layout);
}
