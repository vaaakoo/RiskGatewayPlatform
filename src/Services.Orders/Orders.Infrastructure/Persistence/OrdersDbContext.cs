using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Persistence.Models;

namespace Orders.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var o = modelBuilder.Entity<OrderEntity>();
        o.ToTable("Orders");
        o.HasKey(x => x.Id);
        o.Property(x => x.ClientId).HasMaxLength(128).IsRequired();
        o.Property(x => x.Reference).HasMaxLength(256).IsRequired();
        o.Property(x => x.Amount).HasPrecision(18, 2);
        o.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        o.Property(x => x.Status).HasMaxLength(32).IsRequired();
        o.HasIndex(x => new { x.ClientId, x.CreatedAtUtc });
    }
}
