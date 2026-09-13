using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WooCommerceProductManager.Data;

namespace WooCommerceProductManager.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IDbContextFactory<AppDbContext> factory, ILogger logger)
    {
        await using var context = await factory.CreateDbContextAsync().ConfigureAwait(false);
        await context.Database.MigrateAsync().ConfigureAwait(false);
        logger.LogInformation("SQLite database is ready.");
    }
}
