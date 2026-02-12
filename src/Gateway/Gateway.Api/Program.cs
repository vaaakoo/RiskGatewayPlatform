using BuildingBlocks.Errors;
using BuildingBlocks.Http;
using BuildingBlocks.Logging;
using BuildingBlocks.Observability;
using Gateway.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);
builder.AddSerilogLogging("Gateway");

builder.Services.AddProblemDetails();
builder.Services.AddObservability(builder.Configuration, "Gateway");

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<JwksProvider>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false; // docker/dev
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
                // sync-over-async kept minimal; production: preload keys / background refresh
                var sp = builder.Services.BuildServiceProvider();
                var prov = sp.GetRequiredService<JwksProvider>();
                return prov.GetSigningKeysAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("scope:orders.read", p => p.RequireAssertion(ctx => HasScope(ctx, "orders.read")));
    o.AddPolicy("scope:orders.write", p => p.RequireAssertion(ctx => HasScope(ctx, "orders.write")));
    o.AddPolicy("scope:payments.read", p => p.RequireAssertion(ctx => HasScope(ctx, "payments.read")));
    o.AddPolicy("scope:payments.write", p => p.RequireAssertion(ctx => HasScope(ctx, "payments.write")));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddPolicy("per-client", httpContext =>
    {
        var clientId = httpContext.User.FindFirstValue("client_id") ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 50,
                Window = TimeSpan.FromSeconds(10),
                QueueLimit = 0
            });
    });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(t =>
    {
        // forward correlation id
        t.AddRequestTransform(ctx =>
        {
            if (ctx.HttpContext.Request.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out var v))
                ctx.ProxyRequest.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, (string)v!);

            return ValueTask.CompletedTask;
        });

        // mask auth header in logs -> done via Serilog config usually; here we just avoid copying it anywhere
    });

var app = builder.Build();

app.UseCentralExceptionHandling();
app.UseCorrelationId();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/ready", () => Results.Ok(new { ready = true }));

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// YARP routes will specify AuthorizationPolicy per route (see appsettings.json)
app.MapReverseProxy();

app.Run();

static bool HasScope(AuthorizationHandlerContext ctx, string required)
{
    var scope = ctx.User.FindFirstValue("scope") ?? "";
    var scopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return scopes.Contains(required);
}

public partial class Program { }
