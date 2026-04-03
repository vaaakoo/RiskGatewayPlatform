using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Persistence;

namespace Orders.Api.Extensions;

public static class OrdersDatabaseExtensions
{
    public static async Task UseOrdersDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrdersDbContext>>();

        if (app.Environment.IsEnvironment("Testing"))
        {
            await db.Database.EnsureCreatedAsync();
            return;
        }

        logger.LogInformation("Applying Orders database migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Orders database migrations applied.");
    }
}
