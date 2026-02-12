using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Identity.Api.Security;

public sealed class RsaKeyProvider
{
    // Production: load RSA from Key Vault / mounted volume PEM.
    private readonly RSA _rsa;
    public string KeyId { get; }

    public RsaKeyProvider(IConfiguration cfg)
    {
        // Minimal demo: deterministic KID per startup
        _rsa = RSA.Create(2048);
        KeyId = cfg["JWT_KID"] ?? Guid.NewGuid().ToString("N");
    }

    public RSA Rsa => _rsa;

    public JsonWebKeySet GetJwks()
    {
        var key = new RsaSecurityKey(_rsa) { KeyId = KeyId };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;

        return new JsonWebKeySet
        {
            Keys = { jwk }
        };
    }
}
