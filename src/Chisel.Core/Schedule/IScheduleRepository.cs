namespace Chisel.Core.Schedule;

public interface IScheduleRepository
{
    DaySchedule Get();
    void Save(DaySchedule schedule);
}
