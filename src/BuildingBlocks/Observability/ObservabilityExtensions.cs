using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BuildingBlocks.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration config, string serviceName)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation();
                t.AddHttpClientInstrumentation();

                // OTLP exporter (works with many backends)
                t.AddOtlpExporter(o =>
                {
                    var endpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
                    if (!string.IsNullOrWhiteSpace(endpoint))
                        o.Endpoint = new Uri(endpoint);
                });
            });

        return services;
    }
}
