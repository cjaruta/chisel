using Chisel.Infrastructure.Data;
using Xunit;

namespace Chisel.Infrastructure.Tests.Data;

public class UserSettingsEntityTests : IDisposable
{
    private readonly SqliteConnectionFixture _fixture = new();

    [Fact]
    public void UserSettings_AddAndSave_CanBeReadBack()
    {
        using var context = _fixture.CreateContext();
        var settings = new UserSettingsEntity
        {
            Id = Guid.NewGuid(),
            Name = "Caden",
            WakeTime = new TimeOnly(6, 30),
            BedTime = new TimeOnly(23, 0)
        };

        context.UserSettings.Add(settings);
        context.SaveChanges();

        using var readContext = _fixture.CreateContext();
        var saved = readContext.UserSettings.First(s => s.Id == settings.Id);

        Assert.Equal("Caden", saved.Name);
        Assert.Equal(new TimeOnly(6, 30), saved.WakeTime);
        Assert.Equal(new TimeOnly(23, 0), saved.BedTime);
    }

    public void Dispose() => _fixture.Dispose();
}