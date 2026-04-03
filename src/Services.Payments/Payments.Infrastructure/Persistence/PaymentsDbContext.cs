using Microsoft.EntityFrameworkCore;
using Payments.Infrastructure.Persistence.Models;

namespace Payments.Infrastructure.Persistence;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var p = modelBuilder.Entity<PaymentEntity>();
        p.ToTable("Payments");
        p.HasKey(x => x.Id);
        p.Property(x => x.ClientId).HasMaxLength(128).IsRequired();
        p.Property(x => x.Amount).HasPrecision(18, 2);
        p.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        p.Property(x => x.Status).HasMaxLength(32).IsRequired();
        p.HasIndex(x => new { x.ClientId, x.CreatedAtUtc });
        p.HasIndex(x => x.OrderId);
    }
}
