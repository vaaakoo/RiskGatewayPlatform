using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orders.Application.Abstractions;
using Orders.Infrastructure.Persistence;

namespace Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<OrdersDbContext>(opt =>
        {
            if (environment.IsEnvironment("Testing"))
                opt.UseInMemoryDatabase("OrdersTestDb");
            else
                opt.UseSqlServer(configuration.GetConnectionString("OrdersDb"));
        });

        services.AddScoped<IOrderRepository, OrderRepository>();
        return services;
    }
}
