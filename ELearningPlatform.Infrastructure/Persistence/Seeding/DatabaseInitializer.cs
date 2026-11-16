using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ELearningPlatform.Infrastructure.Persistence.Seeding;

public static class DatabaseInitializer
{
    /// <summary>Applies pending migrations, seeds roles + admin, and (in development) test data.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, bool seedTestData)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        await provider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        await AppDbSeeder.SeedAsync(provider);
        if (seedTestData)
            await DevDataSeeder.SeedAsync(provider);
    }
}
