namespace Chisel.Core.Clock;

// The one saved clock style (replace-all). Get returns the default style until one is saved.
public interface IClockStyleRepository
{
    ClockStyle Get();
    void Save(ClockStyle style);
}
