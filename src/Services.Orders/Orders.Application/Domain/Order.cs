namespace Orders.Application.Domain;

public sealed class Order
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
