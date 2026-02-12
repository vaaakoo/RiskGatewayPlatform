using BuildingBlocks.Errors;
using BuildingBlocks.Http;
using BuildingBlocks.Logging;
using BuildingBlocks.Observability;
using Identity.Api.Security;
using Identity.Application.Security;
using Identity.Application.Tokens;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddSerilogLogging("Identity");

builder.Services.AddProblemDetails();

builder.Services.AddObservability(builder.Configuration, "Identity");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<RsaKeyProvider>();

builder.Services.AddDbContext<IdentityDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("IdentityDb"));
});

builder.Services.AddScoped(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>().Value;
    var rsa = sp.GetRequiredService<RsaKeyProvider>().Rsa;
    return new JwtService(opts, rsa);
});

builder.Services.AddScoped<RefreshTokenService>();

var app = builder.Build();

app.UseCentralExceptionHandling();
app.UseCorrelationId();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/ready", async (IdentityDbContext db) =>
{
    var ok = await db.Database.CanConnectAsync();
    return ok ? Results.Ok(new { ready = true }) : Results.Problem("DB not ready", statusCode: 503);
});

app.MapGet("/.well-known/jwks.json", (RsaKeyProvider kp) =>
{
    var jwks = kp.GetJwks();
    return Results.Json(jwks);
});

app.MapPost("/connect/token", async (
    HttpContext ctx,
    IdentityDbContext db,
    JwtService jwt,
    RefreshTokenService refreshSvc,
    IOptions<JwtOptions> jwtOpts,
    [FromForm] string grant_type,
    [FromForm] string client_id,
    [FromForm] string client_secret,
    [FromForm] string? scope,
    [FromForm] string? refresh_token) =>
{
    var client = await db.Clients.FindAsync(client_id);
    if (client is null || !client.IsActive)
        return Results.Problem("invalid_client", statusCode: 401);

    if (!Hashing.Pbkdf2Verify(client_secret, client.SecretHash))
        return Results.Problem("invalid_client", statusCode: 401);

    var requestedScopes = (scope ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var allowed = client.AllowedScopes();
    if (requestedScopes.Length == 0) requestedScopes = allowed; // default to all allowed
    if (requestedScopes.Any(s => !allowed.Contains(s)))
        return Results.Problem("invalid_scope", statusCode: 400);

    if (grant_type == "client_credentials")
    {
        var (access, exp, jti) = jwt.CreateAccessToken(client_id, requestedScopes);
        // optional: issue refresh on client_credentials (enterprise often wants it) -> we DO, for your requirement
        var sessionId = Guid.NewGuid().ToString("N");
        var refresh = await refreshSvc.IssueAsync(client_id, sessionId, TimeSpan.FromDays(7));

        return Results.Ok(new
        {
            token_type = "Bearer",
            access_token = access,
            expires_in = (int)(exp - DateTimeOffset.UtcNow).TotalSeconds,
            refresh_token = refresh.RawRefreshToken,
            scope = string.Join(' ', requestedScopes),
            jti
        });
    }

    if (grant_type == "refresh_token")
    {
        if (string.IsNullOrWhiteSpace(refresh_token))
            return Results.Problem("missing_refresh_token", statusCode: 400);

        var rotated = await refreshSvc.RotateAsync(client_id, refresh_token, TimeSpan.FromDays(7));
        if (!rotated.Ok)
            return Results.Problem(rotated.Error ?? "invalid_refresh_token", statusCode: 401);

        var (access, exp, jti) = jwt.CreateAccessToken(client_id, requestedScopes);
        return Results.Ok(new
        {
            token_type = "Bearer",
            access_token = access,
            expires_in = (int)(exp - DateTimeOffset.UtcNow).TotalSeconds,
            refresh_token = rotated.NewRawToken,
            scope = string.Join(' ', requestedScopes),
            jti
        });
    }

    return Results.Problem("unsupported_grant_type", statusCode: 400);
});

app.MapPost("/connect/revoke", async (
    IdentityDbContext db,
    RefreshTokenService refreshSvc,
    [FromForm] string client_id,
    [FromForm] string client_secret,
    [FromForm] string token) =>
{
    var client = await db.Clients.FindAsync(client_id);
    if (client is null || !client.IsActive)
        return Results.Problem("invalid_client", statusCode: 401);

    if (!Hashing.Pbkdf2Verify(client_secret, client.SecretHash))
        return Results.Problem("invalid_client", statusCode: 401);

    await refreshSvc.RevokeAsync(client_id, token);
    return Results.Ok(new { revoked = true });
});

app.Run();

public partial class Program { } // for WebApplicationFactory
