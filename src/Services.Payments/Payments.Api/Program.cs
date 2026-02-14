using BuildingBlocks.Errors;
using BuildingBlocks.Http;
using BuildingBlocks.Logging;
using BuildingBlocks.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.AddSerilogLogging("Payments");

builder.Services.AddProblemDetails();
builder.Services.AddObservability(builder.Configuration, "Payments");

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PaymentsJwksProvider>();
builder.Services.AddHttpClient<PaymentsJwksProvider>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "identity",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "gateway",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };

        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = async ctx =>
            {
                var provider = ctx.HttpContext.RequestServices.GetRequiredService<PaymentsJwksProvider>();
                var keys = await provider.GetSigningKeysAsync(ctx.HttpContext.RequestAborted);
                ctx.Options.TokenValidationParameters.IssuerSigningKeys = keys;
            },
            OnAuthenticationFailed = async ctx =>
            {
                if (ctx.Exception is SecurityTokenSignatureKeyNotFoundException)
                {
                    var provider = ctx.HttpContext.RequestServices.GetRequiredService<PaymentsJwksProvider>();
                    provider.InvalidateCache();
                    var keys = await provider.GetSigningKeysAsync(ctx.HttpContext.RequestAborted);
                    ctx.Options.TokenValidationParameters.IssuerSigningKeys = keys;
                }
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("payments.read", p => p.RequireAssertion(ctx => HasScope(ctx, "payments.read")));
    o.AddPolicy("payments.write", p => p.RequireAssertion(ctx => HasScope(ctx, "payments.write")));
});

var app = builder.Build();

try
{
    var jwksProv = app.Services.GetRequiredService<PaymentsJwksProvider>();
    _ = await jwksProv.GetSigningKeysAsync(CancellationToken.None);
}
catch { }

app.UseCentralExceptionHandling();
app.UseCorrelationId();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/payments", () => Results.Ok(new[] { new { id = 1, amount = 100.0m } }))
   .RequireAuthorization("payments.read");

app.MapPost("/payments", () => Results.Created("/payments/1", new { id = 1 }))
   .RequireAuthorization("payments.write");

app.Run();

static bool HasScope(AuthorizationHandlerContext ctx, string required)
{
    var scope = ctx.User.FindFirstValue("scope") ?? "";
    return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(required);
}

public partial class Program { }

sealed class PaymentsJwksProvider(HttpClient http, IMemoryCache cache, IConfiguration cfg)
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
