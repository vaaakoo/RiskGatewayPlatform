using BuildingBlocks.Errors;
using BuildingBlocks.Http;
using BuildingBlocks.Logging;
using BuildingBlocks.Observability;
using Gateway.Api.Extensions;
using Gateway.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);
builder.AddSerilogLogging("Gateway");

builder.Services.AddProblemDetails();
builder.Services.AddObservability(builder.Configuration, "Gateway");

builder.Services.AddGatewayJwtAuth(builder.Configuration);

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("scope:orders.read", p => p.RequireAssertion(ctx => HasScope(ctx, "orders.read")));
    o.AddPolicy("scope:orders.write", p => p.RequireAssertion(ctx => HasScope(ctx, "orders.write")));
    o.AddPolicy("scope:payments.read", p => p.RequireAssertion(ctx => HasScope(ctx, "payments.read")));
    o.AddPolicy("scope:payments.write", p => p.RequireAssertion(ctx => HasScope(ctx, "payments.write")));
});

builder.Services.AddGatewayRateLimiting();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(t =>
    {
        t.AddRequestTransform(ctx =>
        {
            if (ctx.HttpContext.Request.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out var v))
                ctx.ProxyRequest.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, (string)v!);

            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();

// Warmup JWKS cache to reduce first-request latency
try
{
    var jwksProv = app.Services.GetRequiredService<JwksProvider>();
    _ = await jwksProv.GetSigningKeysAsync(CancellationToken.None);
}
catch { /* OK if Identity not yet ready during startup */ }

app.UseCentralExceptionHandling();
app.UseCorrelationId();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/ready", () => Results.Ok(new { ready = true }));

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapReverseProxy();

app.Run();

static bool HasScope(AuthorizationHandlerContext ctx, string required)
{
    var scope = ctx.User.FindFirstValue("scope") ?? "";
    var scopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return scopes.Contains(required);
}

public partial class Program { }
