using System;
using System.Collections.Generic;
using System.Linq;
using Chisel.Core.Clock;

namespace Chisel.App.Services;

// Alarms kept only in memory: what the main view model uses when no real store is given (tests, previews).
public sealed class MemoryAlarmRepository : IAlarmRepository
{
    private readonly List<Alarm> _items = [];

    public IReadOnlyList<Alarm> GetAll() => _items.ToList();

    public void Save(Alarm alarm)
    {
        var i = _items.FindIndex(a => a.Id == alarm.Id);
        if (i < 0)
        {
            _items.Add(alarm);
        }
        else
        {
            _items[i] = alarm;
        }
    }

    public void Delete(Guid id) => _items.RemoveAll(a => a.Id == id);
}
