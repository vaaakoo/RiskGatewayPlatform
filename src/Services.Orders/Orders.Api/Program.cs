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
builder.AddSerilogLogging("Orders");

builder.Services.AddProblemDetails();
builder.Services.AddObservability(builder.Configuration, "Orders");

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<OrdersJwksProvider>();

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
            IssuerSigningKeyResolver = (token, secToken, kid, parms) =>
            {
                var sp = builder.Services.BuildServiceProvider();
                var prov = sp.GetRequiredService<OrdersJwksProvider>();
                return prov.GetSigningKeysAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("orders.read", p => p.RequireAssertion(ctx => HasScope(ctx, "orders.read")));
    o.AddPolicy("orders.write", p => p.RequireAssertion(ctx => HasScope(ctx, "orders.write")));
});

var app = builder.Build();

app.UseCentralExceptionHandling();
app.UseCorrelationId();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/orders", () => Results.Ok(new[] { new { id = 1, item = "demo" } }))
   .RequireAuthorization("orders.read");

app.MapPost("/orders", () => Results.Created("/orders/1", new { id = 1 }))
   .RequireAuthorization("orders.write");

app.Run();

static bool HasScope(AuthorizationHandlerContext ctx, string required)
{
    var scope = ctx.User.FindFirstValue("scope") ?? "";
    return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(required);
}

public partial class Program { }

sealed class OrdersJwksProvider(HttpClient http, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, IConfiguration cfg)
{
    private const string CacheKey = "jwks_keys";
    public async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out IEnumerable<SecurityKey> keys))
            return keys!;

        var jwksUrl = cfg["Identity:JwksUrl"] ?? throw new InvalidOperationException("Identity:JwksUrl missing");
        var jwks = await http.GetFromJsonAsync<JsonWebKeySet>(jwksUrl, cancellationToken: ct)
                   ?? throw new InvalidOperationException("Failed to load JWKS");

        keys = jwks.Keys.Select(k => (SecurityKey)k).ToArray();
        cache.Set(CacheKey, keys, TimeSpan.FromMinutes(5));
        return keys;
    }
}
