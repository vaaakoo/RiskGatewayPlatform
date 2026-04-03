using Payments.Application.Abstractions;
using Shared.Contracts.Orders;

namespace Payments.Api.Testing;

/// <summary>Used in Testing environment so Payments.Api tests do not call a real Orders service.</summary>
public sealed class StubOrdersReadClient : IOrdersReadClient
{
    public Task<OrderResponse?> GetOrderAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult<OrderResponse?>(new OrderResponse(
            orderId,
            "test-order",
            Amount: 100m,
            Currency: "USD",
            Status: "Pending",
            CreatedAtUtc: DateTimeOffset.UtcNow));
}
