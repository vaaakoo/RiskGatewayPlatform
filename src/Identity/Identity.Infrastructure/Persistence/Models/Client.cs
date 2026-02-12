namespace Identity.Infrastructure.Persistence.Models;

public sealed class Client
{
    public string ClientId { get; set; } = default!;
    public string SecretHash { get; set; } = default!;
    public string AllowedScopesCsv { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public string? RateLimitPolicy { get; set; }

    public string[] AllowedScopes()
        => AllowedScopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
