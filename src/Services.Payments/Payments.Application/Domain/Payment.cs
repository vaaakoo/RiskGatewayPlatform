namespace Payments.Application.Domain;

public sealed class Payment
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = "";
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Captured";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
