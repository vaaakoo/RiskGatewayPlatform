using Shared.Contracts.Orders;

namespace Payments.Application.Abstractions;

public interface IOrdersReadClient
{
    Task<OrderResponse?> GetOrderAsync(Guid orderId, CancellationToken ct);
}
