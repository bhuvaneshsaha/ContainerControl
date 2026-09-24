using ContainerControl.Modules.Edge.Domains;
using ContainerControl.Modules.Edge.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Modules.Edge;

/// <summary>
/// Allowed domains and the Traefik container on the edge network.
/// </summary>
public static class EdgeModule
{
    public const string SchemaName = "edge";

    public static IServiceCollection AddEdgeModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Database' is not configured.");
        }

        services.AddDbContext<EdgeDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName);
                npgsql.MigrationsAssembly(typeof(EdgeDbContext).Assembly.GetName().Name);
            }));
        services.AddScoped<EdgeGateway>();
        services.AddScoped<IEdgeGateway>(provider => provider.GetRequiredService<EdgeGateway>());
        return services;
    }
}
