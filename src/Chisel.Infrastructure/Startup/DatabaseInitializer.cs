using Chisel.Infrastructure.Data;
using Chisel.Infrastructure.Repositories;
using Chisel.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Chisel.Infrastructure.Startup;

public static class DatabaseInitializer
{
    public static void Initialize(ChiselDbContext context)
    {
        context.Database.Migrate();

        var settingsRepository = new UserSettingsRepository(context);
        if (settingsRepository.Get() is not null)
        {
            return;
        }

        settingsRepository.Save(SeedData.CreateUserSettings());
        new ScheduleRepository(context).Save(SeedData.CreateSchedule());
        new MarkerRepository(context).ReplaceAll(SeedData.CreateMarkers());
    }
}
