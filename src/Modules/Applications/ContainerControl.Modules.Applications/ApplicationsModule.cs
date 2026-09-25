using ContainerControl.Modules.Applications.Persistence;
using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Applications.Templates;
using ContainerControl.Modules.Applications.Workloads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerControl.Modules.Applications;

/// <summary>
/// Application desired state and secret references. Values go to Infisical.
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
        services.AddHttpClient(nameof(InfisicalSecretStore));
        services.AddScoped<InfisicalSecretStore>();
        services.AddScoped<DevelopmentFileSecretStore>();
        services.AddScoped<ISecretStore, SelectingSecretStore>();
        services.AddScoped<WorkloadAdmin>();
        services.AddScoped<TemplateAdmin>();
        services.AddScoped<IWorkloadStore>(provider => provider.GetRequiredService<WorkloadAdmin>());
        services.AddScoped<ISecretCatalog>(provider => provider.GetRequiredService<WorkloadAdmin>());
        return services;
    }
}
