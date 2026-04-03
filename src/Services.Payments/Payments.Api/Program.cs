using BuildingBlocks.Errors;
using BuildingBlocks.Http;
using BuildingBlocks.Logging;
using BuildingBlocks.Observability;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Payments.Api.Extensions;
using Payments.Api.Testing;
using Payments.Application.Abstractions;
using Payments.Application.Payments;
using Payments.Infrastructure;
using Payments.Infrastructure.Persistence;
using Shared.Contracts.Payments;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.AddSerilogLogging("Payments");

builder.Services.AddProblemDetails();
builder.Services.AddObservability(builder.Configuration, "Payments");

builder.Services.AddPaymentsPersistence(builder.Configuration, builder.Environment);
builder.Services.AddScoped<PaymentService>();

if (builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddSingleton<IOrdersReadClient, StubOrdersReadClient>();
else
    builder.Services.AddOrdersReadIntegration(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IdentityJwksProvider>();

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
                var provider = ctx.HttpContext.RequestServices.GetRequiredService<IdentityJwksProvider>();
                var keys = await provider.GetSigningKeysAsync(ctx.HttpContext.RequestAborted);
                ctx.Options.TokenValidationParameters.IssuerSigningKeys = keys;
            },
            OnAuthenticationFailed = async ctx =>
            {
                if (ctx.Exception is SecurityTokenSignatureKeyNotFoundException)
                {
                    var provider = ctx.HttpContext.RequestServices.GetRequiredService<IdentityJwksProvider>();
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

await app.UsePaymentsDatabaseAsync();

try
{
    var jwksProv = app.Services.GetRequiredService<IdentityJwksProvider>();
    _ = await jwksProv.GetSigningKeysAsync(CancellationToken.None);
}
catch { }

app.UseCentralExceptionHandling();
app.UseCorrelationId();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/ready", async (PaymentsDbContext db) =>
{
    var ok = await db.Database.CanConnectAsync();
    return ok ? Results.Ok(new { ready = true }) : Results.Problem("DB not ready", statusCode: 503);
});

app.MapGet("/payments", async (PaymentService svc, ClaimsPrincipal user, CancellationToken ct) =>
{
    var clientId = ClientId(user);
    var list = await svc.ListAsync(clientId, ct);
    return Results.Ok(list);
}).RequireAuthorization("payments.read");

app.MapGet("/payments/{id:guid}", async (Guid id, PaymentService svc, ClaimsPrincipal user, CancellationToken ct) =>
{
    var clientId = ClientId(user);
    var p = await svc.GetAsync(id, clientId, ct);
    return p is null ? Results.NotFound() : Results.Ok(p);
}).RequireAuthorization("payments.read");

app.MapPost("/payments", async (PaymentService svc, ClaimsPrincipal user, [FromBody] CreatePaymentRequest body, CancellationToken ct) =>
{
    try
    {
        var clientId = ClientId(user);
        var created = await svc.CreateAsync(body, clientId, ct);
        return Results.Created($"/payments/{created.Id}", created);
    }
    catch (ArgumentException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
}).RequireAuthorization("payments.write");

app.Run();

static bool HasScope(AuthorizationHandlerContext ctx, string required)
{
    var scope = ctx.User.FindFirstValue("scope") ?? "";
    return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(required);
}

static string ClientId(ClaimsPrincipal user) =>
    user.FindFirstValue("client_id") ?? user.FindFirstValue("sub")
    ?? throw new InvalidOperationException("Missing client identity claim.");

public partial class Program { }
