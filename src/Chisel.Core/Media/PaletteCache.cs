using Chisel.Core.Theme;

namespace Chisel.Core.Media;

// A small least-recently-used cache of extracted palettes, keyed by a hash of the cover's bytes. Safe to use from any thread.
public sealed class PaletteCache(int capacity)
{
    private readonly LinkedList<(string Key, Rgb[] Palette)> _items = new();
    private readonly Lock _lock = new();

    public Rgb[]? Get(string key)
    {
        lock (_lock)
        {
            for (var node = _items.First; node is not null; node = node.Next)
            {
                if (node.Value.Key == key)
                {
                    _items.Remove(node);
                    _items.AddFirst(node);
                    return node.Value.Palette;
                }
            }
            return null;
        }
    }

    public void Add(string key, Rgb[] palette)
    {
        lock (_lock)
        {
            for (var node = _items.First; node is not null; node = node.Next)
            {
                if (node.Value.Key == key)
                {
                    _items.Remove(node);
                    break;
                }
            }
            _items.AddFirst((key, palette));
            while (_items.Count > capacity)
            {
                _items.RemoveLast();
            }
        }
    }
}
