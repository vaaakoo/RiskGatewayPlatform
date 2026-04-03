namespace Shared.Contracts.Payments;

public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record CreatePaymentRequest(
    Guid OrderId,
    decimal Amount,
    string Currency = "USD");
