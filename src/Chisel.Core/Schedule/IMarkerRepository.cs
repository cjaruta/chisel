namespace Chisel.Core.Schedule;

public interface IMarkerRepository
{
    IReadOnlyList<Marker> GetAll();
    void ReplaceAll(IEnumerable<Marker> markers);
}
