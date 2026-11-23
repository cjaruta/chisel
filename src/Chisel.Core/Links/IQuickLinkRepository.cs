namespace Chisel.Core.Links;

public interface IQuickLinkRepository
{
    IReadOnlyList<QuickLink> GetAll();
    void Add(QuickLink link);
    void Update(QuickLink link);
    void Remove(Guid id);

    // Saves the order of the links: the first id comes first. Ids not listed keep their place after the listed ones.
    void Reorder(IReadOnlyList<Guid> orderedIds);
}
