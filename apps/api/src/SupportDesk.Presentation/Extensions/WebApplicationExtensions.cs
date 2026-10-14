using Microsoft.EntityFrameworkCore;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Applies pending migrations and seeds the demo data. A convenience for a developer
    /// machine and the local container - a real deployment runs migrations as a separate,
    /// deliberate step.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<SupportDbSeeder>().SeedAsync();
    }
}
