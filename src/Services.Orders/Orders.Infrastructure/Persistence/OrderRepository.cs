using Microsoft.EntityFrameworkCore;
using Orders.Application.Abstractions;
using Orders.Application.Domain;
using Orders.Infrastructure.Persistence.Models;

namespace Orders.Infrastructure.Persistence;

public sealed class OrderRepository(OrdersDbContext db) : IOrderRepository
{
    public async Task<IReadOnlyList<Order>> ListByClientAsync(string clientId, CancellationToken ct)
    {
        var rows = await db.Orders.AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<Order?> FindAsync(Guid id, string clientId, CancellationToken ct)
    {
        var row = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        return row is null ? null : ToDomain(row);
    }

    public Task AddAsync(Order order, CancellationToken ct)
    {
        db.Orders.Add(ToEntity(order));
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static Order ToDomain(OrderEntity e) => new()
    {
        Id = e.Id,
        ClientId = e.ClientId,
        Reference = e.Reference,
        Amount = e.Amount,
        Currency = e.Currency,
        Status = e.Status,
        CreatedAtUtc = e.CreatedAtUtc
    };

    private static OrderEntity ToEntity(Order o) => new()
    {
        Id = o.Id,
        ClientId = o.ClientId,
        Reference = o.Reference,
        Amount = o.Amount,
        Currency = o.Currency,
        Status = o.Status,
        CreatedAtUtc = o.CreatedAtUtc
    };
}
