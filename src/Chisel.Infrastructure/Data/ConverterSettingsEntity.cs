namespace Chisel.Infrastructure.Data;

// The one saved set of converter choices (Id is always 1): the category and unit ids from UnitCatalog.
public class ConverterSettingsEntity
{
    public int Id { get; set; } = 1;
    public string Category { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}
