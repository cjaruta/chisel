using Chisel.Core.Converter;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class ConverterSettingsRepository : IConverterSettingsRepository
{
    private readonly ChiselDbContext _context;

    public ConverterSettingsRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public ConverterSettings Get() => _context.ConverterSettings.FirstOrDefault() is { } e
        ? new ConverterSettings(e.Category, e.From, e.To).Valid()
        : ConverterSettings.Default;

    public void Save(ConverterSettings settings)
    {
        _context.ConverterSettings.RemoveRange(_context.ConverterSettings);
        _context.ConverterSettings.Add(new ConverterSettingsEntity { Category = settings.Category, From = settings.From, To = settings.To });
        _context.SaveChanges();
    }
}
