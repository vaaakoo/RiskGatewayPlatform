using Identity.Application.Security;
using Identity.Application.Tokens;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Extensions;

public static class IdentityPersistenceExtensions
{
    public static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddDbContext<IdentityDbContext>(opt =>
        {
            if (environment.IsEnvironment("Testing"))
            {
                opt.UseInMemoryDatabase("TestDb");
            }
            else
            {
                opt.UseSqlServer(configuration.GetConnectionString("IdentityDb"));
            }
        });

        return services;
    }

    public static async Task UseIdentityMigrationsAndSeeding(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IdentityDbContext>>();

        if (app.Environment.IsEnvironment("Testing"))
        {
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            logger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully");
        }

        if (!await db.Clients.AnyAsync())
        {
            logger.LogInformation("Seeding initial clients...");
            SeedClients(db);
            await db.SaveChangesAsync();
            logger.LogInformation("Initial clients seeded successfully");
        }
    }

    private static void SeedClients(IdentityDbContext db)
    {
        var salt1 = new byte[16];
        var salt2 = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(salt1);
        System.Security.Cryptography.RandomNumberGenerator.Fill(salt2);

        db.Clients.Add(new Client
        {
            ClientId = "test-client",
            SecretHash = Hashing.Pbkdf2Hash("test-secret", salt1),
            AllowedScopesCsv = "orders.read,orders.write,payments.read,payments.write",
            IsActive = true,
            RateLimitPolicy = "standard"
        });

        db.Clients.Add(new Client
        {
            ClientId = "gateway-client",
            SecretHash = Hashing.Pbkdf2Hash("gateway-secret", salt2),
            AllowedScopesCsv = "orders.read,orders.write,payments.read,payments.write",
            IsActive = true,
            RateLimitPolicy = "premium"
        });
    }
}
