namespace Chisel.Core.Clock;

// Decides when alarms ring and keeps them saved; knows nothing about the UI. The host calls Check() often (it is cheap: the
// alarms are held in memory); it raises Fired for each alarm that came due since the last check. Alarms only ring while the host
// is running: one whose time passed before the service started, or more than Grace ago (the machine slept through it), is skipped.
public sealed class AlarmService
{
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(10);

    private readonly IAlarmRepository _repository;
    private readonly TimeProvider _time;
    private List<Alarm> _alarms;
    private DateTime _lastCheck;

    public event Action<Alarm>? Fired;

    public AlarmService(IAlarmRepository repository, TimeProvider time)
    {
        _repository = repository;
        _time = time;
        _alarms = repository.GetAll().ToList();
        _lastCheck = _time.GetLocalNow().DateTime;
    }

    // Earliest time of day first.
    public IReadOnlyList<Alarm> Alarms => _alarms.OrderBy(a => a.Time).ThenBy(a => a.Label).ToList();

    public Alarm Add(TimeOnly time, string label, AlarmDays days)
    {
        var alarm = Alarm.Create(time, label, days);
        Store(alarm);
        return alarm;
    }

    // Replaces the alarm with the same id. A changed alarm may ring again today.
    public void Update(Alarm alarm) => Store(alarm with { LastFiredOn = null });

    public void SetEnabled(Guid id, bool enabled)
    {
        if (_alarms.Find(a => a.Id == id) is { } alarm)
        {
            Store(alarm with { Enabled = enabled, LastFiredOn = null });
        }
    }

    public void Delete(Guid id)
    {
        _alarms.RemoveAll(a => a.Id == id);
        _repository.Delete(id);
    }

    public void Check()
    {
        var now = _time.GetLocalNow().DateTime;
        var due = Due(_alarms, _lastCheck, now);
        _lastCheck = now;
        foreach (var alarm in due)
        {
            var fired = alarm with { LastFiredOn = DateOnly.FromDateTime(now), Enabled = alarm.Repeats };
            Store(fired);
            Fired?.Invoke(fired);
        }
    }

    // The enabled alarms whose time fell after `from` and up to `to` (and not longer than Grace before `to`), that have not rung that day.
    public static IReadOnlyList<Alarm> Due(IEnumerable<Alarm> alarms, DateTime from, DateTime to)
    {
        var due = new List<Alarm>();
        foreach (var alarm in alarms.Where(a => a.Enabled))
        {
            for (var date = DateOnly.FromDateTime(from); date <= DateOnly.FromDateTime(to); date = date.AddDays(1))
            {
                var at = date.ToDateTime(alarm.Time);
                if (alarm.OccursOn(date) && alarm.LastFiredOn != date && at > from && at <= to && to - at <= Grace)
                {
                    due.Add(alarm);
                    break;
                }
            }
        }
        return due;
    }

    private void Store(Alarm alarm)
    {
        var i = _alarms.FindIndex(a => a.Id == alarm.Id);
        if (i < 0)
        {
            _alarms.Add(alarm);
        }
        else
        {
            _alarms[i] = alarm;
        }
        _repository.Save(alarm);
    }
}
