using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Http;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "x-correlation-id";

    public async Task Invoke(HttpContext ctx)
    {
        var incoming = ctx.Request.Headers[HeaderName].FirstOrDefault();
        var corrId = string.IsNullOrWhiteSpace(incoming) ? Guid.NewGuid().ToString("N") : incoming!;

        ctx.Items[HeaderName] = corrId;
        ctx.Response.Headers[HeaderName] = corrId;

        using (Serilog.Context.LogContext.PushProperty("correlationId", corrId))
        {
            await next(ctx);
        }
    }
}

public static class CorrelationIdExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
