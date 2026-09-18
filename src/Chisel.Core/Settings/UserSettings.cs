namespace Chisel.Core.Settings;

public class UserSettings
{
    public UserSettings(string name, TimeOnly wakeTime, TimeOnly bedTime)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        }

        if (wakeTime == bedTime)
        {
            throw new ArgumentException("Wake time and bed time cannot be the same.", nameof(wakeTime));
        }

        Name = name.Trim();
        WakeTime = wakeTime;
        BedTime = bedTime;
    }

    public string Name { get; }
    public TimeOnly WakeTime { get; }
    public TimeOnly BedTime { get; }
}