using Chisel.Core.Links;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Mapping;

public static class QuickLinkMapper
{
    public static QuickLinkEntity ToEntity(this QuickLink link) => new()
    {
        Id = link.Id,
        Label = link.Label,
        Url = link.Url
    };

    public static QuickLink ToDomain(this QuickLinkEntity entity) =>
        new(entity.Id, entity.Label, entity.Url);
}