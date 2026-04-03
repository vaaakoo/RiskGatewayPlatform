using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Payments.Application.Abstractions;
using Payments.Infrastructure.Http;
using Payments.Infrastructure.Persistence;

namespace Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<PaymentsDbContext>(opt =>
        {
            if (environment.IsEnvironment("Testing"))
                opt.UseInMemoryDatabase("PaymentsTestDb");
            else
                opt.UseSqlServer(configuration.GetConnectionString("PaymentsDb"));
        });

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        return services;
    }

    public static IServiceCollection AddOrdersReadIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<ForwardAuthDelegatingHandler>();
        services.AddHttpClient<IOrdersReadClient, OrdersReadClient>(client =>
        {
            var baseUrl = configuration["Services:Orders:BaseUrl"]
                          ?? throw new InvalidOperationException("Services:Orders:BaseUrl is not configured.");
            var normalized = baseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(normalized, UriKind.Absolute);
        }).AddHttpMessageHandler<ForwardAuthDelegatingHandler>();

        return services;
    }
}
