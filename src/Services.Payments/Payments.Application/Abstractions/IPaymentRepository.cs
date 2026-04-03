using Payments.Application.Domain;

namespace Payments.Application.Abstractions;

public interface IPaymentRepository
{
    Task<IReadOnlyList<Payment>> ListByClientAsync(string clientId, CancellationToken ct);
    Task<Payment?> FindAsync(Guid id, string clientId, CancellationToken ct);
    Task AddAsync(Payment payment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
