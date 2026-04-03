using Payments.Application.Abstractions;
using Payments.Application.Domain;
using Shared.Contracts.Payments;

namespace Payments.Application.Payments;

public sealed class PaymentService(IPaymentRepository payments, IOrdersReadClient orders)
{
    public async Task<IReadOnlyList<PaymentResponse>> ListAsync(string clientId, CancellationToken ct)
    {
        var list = await payments.ListByClientAsync(clientId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<PaymentResponse?> GetAsync(Guid id, string clientId, CancellationToken ct)
    {
        var p = await payments.FindAsync(id, clientId, ct);
        return p is null ? null : Map(p);
    }

    public async Task<PaymentResponse> CreateAsync(CreatePaymentRequest request, string clientId, CancellationToken ct)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Currency))
            throw new ArgumentException("Currency is required.", nameof(request));

        var order = await orders.GetOrderAsync(request.OrderId, ct);
        if (order is null)
            throw new InvalidOperationException("Order not found or not accessible.");

        var currency = request.Currency.Trim();
        if (order.Amount != request.Amount
            || !string.Equals(order.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Payment amount and currency must match the order.");

        var now = DateTimeOffset.UtcNow;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            OrderId = request.OrderId,
            Amount = request.Amount,
            Currency = currency.ToUpperInvariant(),
            Status = "Captured",
            CreatedAtUtc = now
        };

        await payments.AddAsync(payment, ct);
        await payments.SaveChangesAsync(ct);
        return Map(payment);
    }

    private static PaymentResponse Map(Payment p) =>
        new(p.Id, p.OrderId, p.Amount, p.Currency, p.Status, p.CreatedAtUtc);
}
