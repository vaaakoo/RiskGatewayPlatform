using Identity.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Client>(e =>
        {
            e.HasKey(x => x.ClientId);
            e.Property(x => x.ClientId).HasMaxLength(100);
            e.Property(x => x.SecretHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.AllowedScopesCsv).HasMaxLength(2000).IsRequired();
            e.Property(x => x.RateLimitPolicy).HasMaxLength(100);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();

            e.Property(x => x.ClientId).HasMaxLength(100).IsRequired();
            e.Property(x => x.SessionId).HasMaxLength(50).IsRequired();
            e.Property(x => x.TokenHash).HasMaxLength(500).IsRequired();

            e.HasIndex(x => new { x.ClientId, x.SessionId });
        });
    }
}
