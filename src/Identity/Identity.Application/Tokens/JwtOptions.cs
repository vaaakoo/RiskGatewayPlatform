namespace Identity.Application.Tokens;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "identity";
    public string Audience { get; set; } = "gateway";
    public int AccessTokenMinutes { get; set; } = 10;
}
