using Orders.Application.Domain;

namespace Orders.Application.Abstractions;

public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> ListByClientAsync(string clientId, CancellationToken ct);
    Task<Order?> FindAsync(Guid id, string clientId, CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
