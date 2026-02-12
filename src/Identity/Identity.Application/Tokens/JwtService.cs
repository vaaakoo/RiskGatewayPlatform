using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Identity.Application.Tokens;

public sealed class JwtService(JwtOptions options, RSA rsa)
{
    public (string token, DateTimeOffset expiresAt, string jti) CreateAccessToken(string clientId, string[] scopes)
    {
        var now = DateTimeOffset.UtcNow;
        var exp = now.AddMinutes(options.AccessTokenMinutes);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new("sub", clientId),
            new("client_id", clientId),
            new("scope", string.Join(' ', scopes)),
            new("jti", jti),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        var creds = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

        var jwt = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: exp.UtcDateTime,
            signingCredentials: creds);

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return (token, exp, jti);
    }
}
