using Chisel.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Chisel.Infrastructure.Tests.Data;

public class SqliteConnectionFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteConnectionFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ChiselDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ChiselDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new ChiselDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}