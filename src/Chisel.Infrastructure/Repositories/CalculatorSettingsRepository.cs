using Chisel.Core.Calculator;
using Chisel.Infrastructure.Data;

namespace Chisel.Infrastructure.Repositories;

public class CalculatorSettingsRepository : ICalculatorSettingsRepository
{
    private readonly ChiselDbContext _context;

    public CalculatorSettingsRepository(ChiselDbContext context)
    {
        _context = context;
    }

    public CalculatorSettings Get() => _context.CalculatorSettings.FirstOrDefault() is { } e
        ? new CalculatorSettings(e.Advanced, Enum.IsDefined((AngleMode)e.Angle) ? (AngleMode)e.Angle : AngleMode.Degrees)
        : CalculatorSettings.Default;

    public void Save(CalculatorSettings settings)
    {
        _context.CalculatorSettings.RemoveRange(_context.CalculatorSettings);
        _context.CalculatorSettings.Add(new CalculatorSettingsEntity { Advanced = settings.Advanced, Angle = (int)settings.Angle });
        _context.SaveChanges();
    }
}
