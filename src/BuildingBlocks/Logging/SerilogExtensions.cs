using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace BuildingBlocks.Logging;

public static class SerilogExtensions
{
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder, string serviceName)
    {
        var seqUrl = builder.Configuration["SEQ_URL"];

        builder.Host.UseSerilog((ctx, lc) =>
        {
            lc.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
              .Enrich.FromLogContext()
              .Enrich.WithProperty("service", serviceName)
              .WriteTo.Console(theme: AnsiConsoleTheme.Code);

            if (!string.IsNullOrWhiteSpace(seqUrl))
                lc.WriteTo.Seq(seqUrl);
        });

        return builder;
    }
}
