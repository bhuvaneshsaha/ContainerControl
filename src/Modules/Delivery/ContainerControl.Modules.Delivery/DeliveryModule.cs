using ContainerControl.Modules.Delivery.Persistence;
using ContainerControl.Modules.Delivery.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Modules.Delivery;

/// <summary>
/// Compose policy, deploy, rollback, and the worker lease.
/// </summary>
public static class DeliveryModule
{
    public const string SchemaName = "delivery";

    public static IServiceCollection AddDeliveryModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Database' is not configured.");
        }

        services.AddDbContext<DeliveryDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName);
                npgsql.MigrationsAssembly(typeof(DeliveryDbContext).Assembly.GetName().Name);
            }));
        services.AddScoped<DeployService>();
        return services;
    }
}
