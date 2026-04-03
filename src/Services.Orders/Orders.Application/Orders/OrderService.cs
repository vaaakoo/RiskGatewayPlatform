using Orders.Application.Abstractions;
using Orders.Application.Domain;
using Shared.Contracts.Orders;

namespace Orders.Application.Orders;

public sealed class OrderService(IOrderRepository orders)
{
    public async Task<IReadOnlyList<OrderResponse>> ListAsync(string clientId, CancellationToken ct)
    {
        var list = await orders.ListByClientAsync(clientId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<OrderResponse?> GetAsync(Guid id, string clientId, CancellationToken ct)
    {
        var o = await orders.FindAsync(id, clientId, ct);
        return o is null ? null : Map(o);
    }

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, string clientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reference))
            throw new ArgumentException("Reference is required.", nameof(request));
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Currency))
            throw new ArgumentException("Currency is required.", nameof(request));

        var now = DateTimeOffset.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Reference = request.Reference.Trim(),
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Status = "Pending",
            CreatedAtUtc = now
        };

        await orders.AddAsync(order, ct);
        await orders.SaveChangesAsync(ct);
        return Map(order);
    }

    private static OrderResponse Map(Order o) =>
        new(o.Id, o.Reference, o.Amount, o.Currency, o.Status, o.CreatedAtUtc);
}
