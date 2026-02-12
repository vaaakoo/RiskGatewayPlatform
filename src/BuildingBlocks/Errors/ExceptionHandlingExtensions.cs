using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlocks.Errors;

public static class ExceptionHandlingExtensions
{
    public static WebApplication UseCentralExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(handler =>
        {
            handler.Run(async ctx =>
            {
                var feature = ctx.Features.Get<IExceptionHandlerFeature>();
                var ex = feature?.Error;

                var pd = new ProblemDetails
                {
                    Title = "Unhandled error",
                    Detail = ex?.Message,
                    Status = StatusCodes.Status500InternalServerError
                };

                ctx.Response.StatusCode = pd.Status.Value;
                ctx.Response.ContentType = "application/problem+json";
                await ctx.Response.WriteAsJsonAsync(pd);
            });
        });

        return app;
    }
}
