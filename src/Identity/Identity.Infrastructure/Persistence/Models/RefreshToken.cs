namespace Identity.Infrastructure.Persistence.Models;

public sealed class RefreshToken
{
    public long Id { get; set; }

    public string ClientId { get; set; } = default!;

    // A “token family” / session for reuse detection
    public string SessionId { get; set; } = default!;

    // Never store raw refresh token
    public string TokenHash { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    // Rotation
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    // Reuse detection -> session kill switch
    public DateTimeOffset? SessionRevokedAt { get; set; }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;
    public bool IsRevoked() => RevokedAt != null || SessionRevokedAt != null;
}
