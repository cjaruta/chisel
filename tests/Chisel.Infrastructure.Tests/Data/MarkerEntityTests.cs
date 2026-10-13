using Chisel.Infrastructure.Data;
using Xunit;
using Chisel.Core.Schedule;

namespace Chisel.Infrastructure.Tests.Data;

public class MarkerEntityTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void Markers_AddAndSave_CanBeReadBack()
    {
        using var context = _fixture.CreateContext();
        var marker = new MarkerEntity
        {
            Id = Guid.NewGuid(),
            Label = "Last coffee",
            Time = new TimeOnly(13, 0),
            Kind = MarkerKind.Reminder
        };

        context.Markers.Add(marker);
        context.SaveChanges();

        using var readContext = _fixture.CreateContext();
        var saved = readContext.Markers.First(m => m.Id == marker.Id);

        Assert.Equal("Last coffee", saved.Label);
        Assert.Equal(new TimeOnly(13, 0), saved.Time);
        Assert.Equal(MarkerKind.Reminder, saved.Kind);
    }

    public void Dispose() => _fixture.Dispose();
}