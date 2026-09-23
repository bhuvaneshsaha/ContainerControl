using ContainerControl.Modules.Runtime.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Modules.Runtime;

/// <summary>
/// Logs, stats, and lifecycle. No Engine calls in this slice.
/// </summary>
public static class RuntimeModule
{
    public const string SchemaName = "runtime";

    public static IServiceCollection AddRuntimeModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Database' is not configured.");
        }

        services.AddDbContext<RuntimeDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName);
                npgsql.MigrationsAssembly(typeof(RuntimeDbContext).Assembly.GetName().Name);
            }));
        return services;
    }
}
