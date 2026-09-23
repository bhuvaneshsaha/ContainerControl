using System.Reflection;
using ContainerControl.Modules.Access.Infrastructure.Persistence;
using ContainerControl.Modules.Access.Infrastructure.Seeding;
using ContainerControl.Modules.Applications.Persistence;
using ContainerControl.Modules.Delivery.Persistence;
using ContainerControl.Modules.Edge.Persistence;
using ContainerControl.Modules.Platform.Persistence;
using ContainerControl.Modules.Registries.Persistence;
using ContainerControl.Modules.Runtime.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContainerControl.Host.Hosting;

public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        ILogger<DatabaseInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsEfDesignTime())
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await MigrateAsync<AccessDbContext>(services, cancellationToken);
        await MigrateAsync<PlatformDbContext>(services, cancellationToken);
        await MigrateAsync<RegistriesDbContext>(services, cancellationToken);
        await MigrateAsync<ApplicationsDbContext>(services, cancellationToken);
        await MigrateAsync<DeliveryDbContext>(services, cancellationToken);
        await MigrateAsync<EdgeDbContext>(services, cancellationToken);
        await MigrateAsync<RuntimeDbContext>(services, cancellationToken);

        if (_environment.IsDevelopment())
        {
            await services.GetRequiredService<DevelopmentAccessSeeder>().SeedAsync(cancellationToken);
            _logger.LogInformation("Development access seed finished.");
        }

        await services.GetRequiredService<FirstAdminBootstrap>().EnsureAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task MigrateAsync<TContext>(IServiceProvider services, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var context = services.GetRequiredService<TContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    private static bool IsEfDesignTime() =>
        string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "ef", StringComparison.OrdinalIgnoreCase);
}
