using System.Security.Cryptography;
using System.Text;

namespace Identity.Application.Security;

public static class Hashing
{
    public static string Sha256Base64(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    // For client secrets (PBKDF2)
    public static string Pbkdf2Hash(string secret, byte[] salt, int iterations = 100_000)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(secret, salt, iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(32);
        return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Pbkdf2Verify(string secret, string stored)
    {
        var parts = stored.Split('.');
        var iterations = int.Parse(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);

        using var pbkdf2 = new Rfc2898DeriveBytes(secret, salt, iterations, HashAlgorithmName.SHA256);
        var actual = pbkdf2.GetBytes(32);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
