using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Identity.Api.Security;

public sealed class RsaKeyProvider
{
    private readonly RSA _rsa;
    public string KeyId { get; }

    public RsaKeyProvider(IConfiguration cfg, IWebHostEnvironment env)
    {
        _rsa = RSA.Create(2048);

        if (env.IsEnvironment("Testing"))
        {
            // In-memory key for tests; no file I/O
            KeyId = cfg["JWT_KID"] ?? "test-kid";
        }
        else
        {
            var keysPath = cfg["KEYS_PATH"] ?? Path.Combine(AppContext.BaseDirectory, "keys");
            Directory.CreateDirectory(keysPath);
            var pemPath = Path.Combine(keysPath, "private_key.pem");

            if (File.Exists(pemPath))
            {
                var pem = File.ReadAllText(pemPath);
                _rsa.ImportFromPem(pem);
            }
            else
            {
                File.WriteAllText(pemPath, _rsa.ExportPkcs8PrivateKeyPem());
            }

            KeyId = cfg["JWT_KID"] ?? DeriveKid(_rsa);
        }
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

    private static string DeriveKid(RSA rsa)
    {
        var pub = rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(pub);
        return Convert.ToHexStringLower(hash)[..16];
    }
}
