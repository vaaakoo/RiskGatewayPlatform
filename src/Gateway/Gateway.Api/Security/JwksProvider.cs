using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;

namespace Gateway.Api.Security;

public sealed class JwksProvider(HttpClient http, IMemoryCache cache, IConfiguration cfg)
{
    private const string CacheKey = "jwks_keys";

    public async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out IEnumerable<SecurityKey>? keys) && keys is not null)
            return keys;

        var jwksUrl = cfg["Identity:JwksUrl"] ?? throw new InvalidOperationException("Identity:JwksUrl missing");
        var jwks = await http.GetFromJsonAsync<JsonWebKeySet>(jwksUrl, cancellationToken: ct)
                   ?? throw new InvalidOperationException("Failed to load JWKS");

        keys = jwks.Keys.Select(k => (SecurityKey)k).ToArray();
        cache.Set(CacheKey, keys, TimeSpan.FromMinutes(5));
        return keys;
    }

    public void InvalidateCache() => cache.Remove(CacheKey);
}
