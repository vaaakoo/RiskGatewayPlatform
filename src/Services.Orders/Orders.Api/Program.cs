using BuildingBlocks.Errors;
using BuildingBlocks.Http;
using BuildingBlocks.Logging;
using BuildingBlocks.Observability;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Orders.Api.Extensions;
using Orders.Application.Orders;
using Orders.Infrastructure;
using Orders.Infrastructure.Persistence;
using Shared.Contracts.Orders;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.AddSerilogLogging("Orders");

builder.Services.AddProblemDetails();
builder.Services.AddObservability(builder.Configuration, "Orders");

builder.Services.AddOrdersPersistence(builder.Configuration, builder.Environment);
builder.Services.AddScoped<OrderService>();

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
    o.AddPolicy("orders.read", p => p.RequireAssertion(ctx => HasScope(ctx, "orders.read")));
    o.AddPolicy("orders.write", p => p.RequireAssertion(ctx => HasScope(ctx, "orders.write")));
});

var app = builder.Build();

await app.UseOrdersDatabaseAsync();

try
{
    var jwksProv = app.Services.GetRequiredService<IdentityJwksProvider>();
    _ = await jwksProv.GetSigningKeysAsync(CancellationToken.None);
}
catch { /* Identity may be unavailable during startup */ }

app.UseCentralExceptionHandling();
app.UseCorrelationId();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/ready", async (OrdersDbContext db) =>
{
    var ok = await db.Database.CanConnectAsync();
    return ok ? Results.Ok(new { ready = true }) : Results.Problem("DB not ready", statusCode: 503);
});

app.MapGet("/orders", async (OrderService svc, ClaimsPrincipal user, CancellationToken ct) =>
{
    var clientId = ClientId(user);
    var list = await svc.ListAsync(clientId, ct);
    return Results.Ok(list);
}).RequireAuthorization("orders.read");

app.MapGet("/orders/{id:guid}", async (Guid id, OrderService svc, ClaimsPrincipal user, CancellationToken ct) =>
{
    var clientId = ClientId(user);
    var o = await svc.GetAsync(id, clientId, ct);
    return o is null ? Results.NotFound() : Results.Ok(o);
}).RequireAuthorization("orders.read");

app.MapPost("/orders", async (OrderService svc, ClaimsPrincipal user, [FromBody] CreateOrderRequest body, CancellationToken ct) =>
{
    try
    {
        var clientId = ClientId(user);
        var created = await svc.CreateAsync(body, clientId, ct);
        return Results.Created($"/orders/{created.Id}", created);
    }
    catch (ArgumentException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
}).RequireAuthorization("orders.write");

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
