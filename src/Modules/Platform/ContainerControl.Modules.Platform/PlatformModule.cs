using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.Modules.Platform.Persistence;
using ContainerControl.Modules.Platform.Quotas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Modules.Platform;

/// <summary>
/// Docker hosts, team CPU, memory, and storage quotas, and host capacity readings. Allowed domains live in Edge.
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
        services.AddSingleton<IDockerEngine, DockerEngineClient>();
        services.AddScoped<HostRegistry>();
        services.AddScoped<IDockerHostLookup>(provider => provider.GetRequiredService<HostRegistry>());
        services.AddScoped<QuotaAdmin>();
        services.AddScoped<ITeamQuotaLookup>(provider => provider.GetRequiredService<QuotaAdmin>());
        return services;
    }
}
