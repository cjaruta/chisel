namespace Chisel.Core.Links;

public interface IQuickLinkRepository
{
    IReadOnlyList<QuickLink> GetAll();
    void Add(QuickLink link);
    void Remove(Guid id);
}
