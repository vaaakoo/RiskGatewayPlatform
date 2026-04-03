namespace Shared.Contracts.Orders;

public sealed record OrderResponse(
    Guid Id,
    string Reference,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateOrderRequest(
    string Reference,
    decimal Amount,
    string Currency = "USD");
