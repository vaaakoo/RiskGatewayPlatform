using Identity.Application.Security;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Identity.Application.Tokens;

public sealed class RefreshTokenService(IdentityDbContext db)
{
    public sealed record IssueResult(string RawRefreshToken, DateTimeOffset ExpiresAt, string SessionId);

    public async Task<IssueResult> IssueAsync(string clientId, string sessionId, TimeSpan lifetime)
    {
        var raw = GenerateSecureToken();
        var hash = Hashing.Sha256Base64(raw);

        var now = DateTimeOffset.UtcNow;
        var entity = new RefreshToken
        {
            ClientId = clientId,
            SessionId = sessionId,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        };

        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync();

        return new IssueResult(raw, entity.ExpiresAt, sessionId);
    }

    public sealed record RotateResult(bool Ok, string? NewRawToken, DateTimeOffset? NewExpiresAt, string? SessionId, string? Error);

    public async Task<RotateResult> RotateAsync(string clientId, string presentedRawToken, TimeSpan lifetime)
    {
        var now = DateTimeOffset.UtcNow;
        var presentedHash = Hashing.Sha256Base64(presentedRawToken);

        var token = await db.RefreshTokens
            .SingleOrDefaultAsync(x => x.ClientId == clientId && x.TokenHash == presentedHash);

        if (token is null)
            return new RotateResult(false, null, null, null, "invalid_refresh_token");

        // If already used / rotated -> reuse detection => revoke entire session
        if (token.IsRevoked())
        {
            await RevokeSessionAsync(clientId, token.SessionId);
            return new RotateResult(false, null, null, token.SessionId, "refresh_token_reuse_detected");
        }

        if (token.IsExpired(now))
        {
            token.RevokedAt = now;
            await db.SaveChangesAsync();
            return new RotateResult(false, null, null, token.SessionId, "refresh_token_expired");
        }

        // Rotate: revoke old, issue new in same session
        var newRaw = GenerateSecureToken();
        var newHash = Hashing.Sha256Base64(newRaw);

        token.RevokedAt = now;
        token.ReplacedByTokenHash = newHash;

        db.RefreshTokens.Add(new RefreshToken
        {
            ClientId = clientId,
            SessionId = token.SessionId,
            TokenHash = newHash,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        });

        await db.SaveChangesAsync();
        return new RotateResult(true, newRaw, now.Add(lifetime), token.SessionId, null);
    }

    public async Task RevokeAsync(string clientId, string presentedRawToken)
    {
        var now = DateTimeOffset.UtcNow;
        var hash = Hashing.Sha256Base64(presentedRawToken);

        var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.ClientId == clientId && x.TokenHash == hash);
        if (token is null) return;

        // revoke whole session (strong default)
        await RevokeSessionAsync(clientId, token.SessionId);
    }

    private async Task RevokeSessionAsync(string clientId, string sessionId)
    {
        var now = DateTimeOffset.UtcNow;

        var tokens = await db.RefreshTokens
            .Where(x => x.ClientId == clientId && x.SessionId == sessionId && x.SessionRevokedAt == null)
            .ToListAsync();

        foreach (var t in tokens)
            t.SessionRevokedAt = now;

        await db.SaveChangesAsync();
    }

    private static string GenerateSecureToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode(bytes.ToArray());
    }
}
