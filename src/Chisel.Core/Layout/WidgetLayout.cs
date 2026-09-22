namespace Chisel.Core.Layout;

public enum Column
{
    Side,
    Main
}

public class WidgetLayout
{
    private readonly List<string> _side = new();
    private readonly List<string> _main = new();

    public IReadOnlyList<string> Side => _side;
    public IReadOnlyList<string> Main => _main;

    public void Move(string widgetId, Column column, int index)
    {
        _side.Remove(widgetId);
        _main.Remove(widgetId);

        var target = column == Column.Side ? _side : _main;
        target.Insert(index, widgetId);
    }

    public void Remove(string widgetId)
    {
        _side.Remove(widgetId);
        _main.Remove(widgetId);
    }

    public void Reset(IEnumerable<string> defaultSide, IEnumerable<string> defaultMain)
    {
        _side.Clear();
        _side.AddRange(defaultSide);

        _main.Clear();
        _main.AddRange(defaultMain);
    }
}