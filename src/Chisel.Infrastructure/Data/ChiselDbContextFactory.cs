using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chisel.Infrastructure.Data;

public class ChiselDbContextFactory : IDesignTimeDbContextFactory<ChiselDbContext>
{
    public ChiselDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ChiselDbContext>()
            .UseSqlite("Data Source=chisel.db")
            .Options;

        return new ChiselDbContext(options);
    }
}