using Microsoft.AspNetCore.RateLimiting;
using System.Collections.Frozen;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Gateway.Api.Extensions;

public static class GatewayRateLimitExtensions
{
    private static readonly FrozenDictionary<string, (int Limit, TimeSpan Window)> Policies =
        new Dictionary<string, (int, TimeSpan)>
        {
            ["standard"] = (50, TimeSpan.FromSeconds(10)),
            ["premium"] = (200, TimeSpan.FromSeconds(10)),
            ["strict"] = (10, TimeSpan.FromSeconds(10)),
        }.ToFrozenDictionary();

    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;

            options.AddPolicy("per-client", httpContext =>
            {
                var clientId = httpContext.User.FindFirstValue("client_id") ?? "anonymous";
                var policyName = httpContext.User.FindFirstValue("rate_limit_policy") ?? "standard";

                var (limit, window) = Policies.GetValueOrDefault(policyName, Policies["standard"]);

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: clientId,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = window,
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }
}
