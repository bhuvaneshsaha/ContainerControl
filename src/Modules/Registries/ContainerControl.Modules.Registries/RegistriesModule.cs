using ContainerControl.Modules.Registries.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Modules.Registries;

/// <summary>
/// Registry connections. No registry calls in this slice.
/// </summary>
public static class RegistriesModule
{
    public const string SchemaName = "registries";

    public static IServiceCollection AddRegistriesModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Database' is not configured.");
        }

        services.AddDbContext<RegistriesDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName);
                npgsql.MigrationsAssembly(typeof(RegistriesDbContext).Assembly.GetName().Name);
            }));
        return services;
    }
}
