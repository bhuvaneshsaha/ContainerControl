using ContainerControl.Modules.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Modules.Platform;

/// <summary>
/// Docker hosts, quotas, allowed domains, and capacity.
/// </summary>
public static class PlatformModule
{
    public const string SchemaName = "platform";

    public static IServiceCollection AddPlatformModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Database' is not configured.");
        }

        services.AddDbContext<PlatformDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName);
                npgsql.MigrationsAssembly(typeof(PlatformDbContext).Assembly.GetName().Name);
            }));
        return services;
    }
}
