using ContainerControl.Modules.Applications.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Modules.Applications;

/// <summary>
/// Application desired state. No app editor in this slice.
/// </summary>
public static class ApplicationsModule
{
    public const string SchemaName = "applications";

    public static IServiceCollection AddApplicationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Database' is not configured.");
        }

        services.AddDbContext<ApplicationsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName);
                npgsql.MigrationsAssembly(typeof(ApplicationsDbContext).Assembly.GetName().Name);
            }));
        return services;
    }
}
