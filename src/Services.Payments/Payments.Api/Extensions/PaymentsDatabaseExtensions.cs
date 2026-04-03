using Microsoft.EntityFrameworkCore;
using Payments.Infrastructure.Persistence;

namespace Payments.Api.Extensions;

public static class PaymentsDatabaseExtensions
{
    public static async Task UsePaymentsDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentsDbContext>>();

        if (app.Environment.IsEnvironment("Testing"))
        {
            await db.Database.EnsureCreatedAsync();
            return;
        }

        logger.LogInformation("Applying Payments database migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Payments database migrations applied.");
    }
}
