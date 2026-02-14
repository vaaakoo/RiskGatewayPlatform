using Gateway.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Api.Extensions;

public static class GatewayAuthExtensions
{
    public static IServiceCollection AddGatewayJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<JwksProvider>();
        services.AddHttpClient<JwksProvider>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "identity",
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"] ?? "gateway",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };

                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = async ctx =>
                    {
                        var provider = ctx.HttpContext.RequestServices.GetRequiredService<JwksProvider>();
                        var keys = await provider.GetSigningKeysAsync(ctx.HttpContext.RequestAborted);
                        ctx.Options.TokenValidationParameters.IssuerSigningKeys = keys;
                    },
                    OnAuthenticationFailed = async ctx =>
                    {
                        if (ctx.Exception is SecurityTokenSignatureKeyNotFoundException)
                        {
                            var provider = ctx.HttpContext.RequestServices.GetRequiredService<JwksProvider>();
                            provider.InvalidateCache();
                            var keys = await provider.GetSigningKeysAsync(ctx.HttpContext.RequestAborted);
                            ctx.Options.TokenValidationParameters.IssuerSigningKeys = keys;
                        }
                    }
                };
            });

        return services;
    }
}
