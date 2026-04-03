using Microsoft.EntityFrameworkCore;
using Payments.Application.Abstractions;
using Payments.Application.Domain;
using Payments.Infrastructure.Persistence.Models;

namespace Payments.Infrastructure.Persistence;

public sealed class PaymentRepository(PaymentsDbContext db) : IPaymentRepository
{
    public async Task<IReadOnlyList<Payment>> ListByClientAsync(string clientId, CancellationToken ct)
    {
        var rows = await db.Payments.AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<Payment?> FindAsync(Guid id, string clientId, CancellationToken ct)
    {
        var row = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        return row is null ? null : ToDomain(row);
    }

    public Task AddAsync(Payment payment, CancellationToken ct)
    {
        db.Payments.Add(ToEntity(payment));
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static Payment ToDomain(PaymentEntity e) => new()
    {
        Id = e.Id,
        ClientId = e.ClientId,
        OrderId = e.OrderId,
        Amount = e.Amount,
        Currency = e.Currency,
        Status = e.Status,
        CreatedAtUtc = e.CreatedAtUtc
    };

    private static PaymentEntity ToEntity(Payment p) => new()
    {
        Id = p.Id,
        ClientId = p.ClientId,
        OrderId = p.OrderId,
        Amount = p.Amount,
        Currency = p.Currency,
        Status = p.Status,
        CreatedAtUtc = p.CreatedAtUtc
    };
}
