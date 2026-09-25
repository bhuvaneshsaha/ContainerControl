using ContainerControl.Modules.Applications.Secrets;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Hosts;
using ContainerControl.Modules.Runtime.Persistence;
using ContainerControl.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContainerControl.Modules.Runtime.Inspection;

public sealed class LogCollector : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LogCollector> _logger;

    public LogCollector(
        IServiceScopeFactory scopes,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<LogCollector> logger)
    {
        _scopes = scopes;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_environment.IsEnvironment("Testing"))
        {
            return;
        }

        var poll = PollInterval();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning("Stored log collection did not finish. {ExceptionType}", exception.GetType().Name);
            }

            await Task.Delay(poll, stoppingToken);
        }
    }

    public async Task CollectOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var apps = await services.GetRequiredService<IWorkloadStore>().ListAllAsync(cancellationToken);
        var hosts = services.GetRequiredService<IDockerHostLookup>();
        var engine = services.GetRequiredService<IDockerEngine>();
        var catalog = services.GetRequiredService<ISecretCatalog>();
        var secrets = services.GetRequiredService<ISecretStore>();
        var db = services.GetRequiredService<RuntimeDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var retention = 14;
        if (int.TryParse(_configuration["Logs:RetentionDays"], out var configured) && configured > 0)
        {
            retention = configured;
        }

        var cutoff = StoredLogText.Cutoff(clock.UtcNow, retention);
        await db.LogLines.Where(line => line.RecordedAtUtc < cutoff).ExecuteDeleteAsync(cancellationToken);

        foreach (var app in apps)
        {
            if (app.Status is "registered" or "pending-approval" or "rejected")
            {
                continue;
            }

            var host = await hosts.FindAsync(app.HostId, cancellationToken);
            if (host is null)
            {
                continue;
            }

            var redactions = await SecretValuesAsync(catalog, secrets, app, cancellationToken);
            var endpoint = new DockerEndpoint(host.Endpoint);
            IReadOnlyList<EngineContainer> containers;
            try
            {
                containers = await engine.ListByLabelAsync(endpoint, ManagedLabels.Application + "=" + app.Id, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning("Stored logs skipped {ApplicationId}. {ExceptionType}", app.Id, exception.GetType().Name);
                continue;
            }

            foreach (var container in containers)
            {
                if (string.Equals(container.Label(ManagedLabels.Role), ManagedLabels.SlotRouterRole, StringComparison.Ordinal)
                    || string.Equals(container.Label(ManagedLabels.Slot), ManagedLabels.RouteSlot, StringComparison.Ordinal))
                {
                    continue;
                }

                await StoreContainerAsync(db, engine, endpoint, app.Id, container, redactions, cancellationToken);
            }
        }
    }

    private async Task StoreContainerAsync(
        RuntimeDbContext db,
        IDockerEngine engine,
        DockerEndpoint endpoint,
        Guid applicationId,
        EngineContainer container,
        IReadOnlyList<string> redactions,
        CancellationToken cancellationToken)
    {
        var cursor = await db.LogCursors.SingleOrDefaultAsync(item => item.ContainerId == container.Id, cancellationToken);
        string raw;
        try
        {
            raw = await engine.ReadTimestampedLogsAsync(endpoint, container.Id, cursor?.LastAtUtc, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Stored logs skipped container {ContainerId}. {ExceptionType}", container.Id, exception.GetType().Name);
            return;
        }

        var parsed = TimestampedLog.Parse(raw, cursor?.LastAtUtc);
        if (parsed.Count == 0)
        {
            return;
        }

        var service = container.Label(ManagedLabels.Service) ?? container.Name.TrimStart('/');
        var latest = cursor?.LastAtUtc ?? DateTimeOffset.MinValue;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in parsed)
        {
            var text = StoredLogText.Clip(LogRedactor.Apply(line.Text, redactions));
            var hash = StoredLogText.Hash(text);
            if (!seen.Add(line.At.UtcTicks + ":" + hash))
            {
                continue;
            }

            var exists = await db.LogLines.AsNoTracking().AnyAsync(
                item => item.ContainerId == container.Id && item.RecordedAtUtc == line.At && item.TextHash == hash,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            db.LogLines.Add(new StoredLogLine
            {
                ApplicationId = applicationId,
                ContainerId = container.Id,
                Service = service.Length > 128 ? service[..128] : service,
                RecordedAtUtc = line.At,
                Text = text,
                TextHash = hash
            });
            if (line.At > latest)
            {
                latest = line.At;
            }
        }

        if (cursor is null)
        {
            db.LogCursors.Add(new LogCursor { ContainerId = container.Id, LastAtUtc = latest });
        }
        else
        {
            cursor.LastAtUtc = latest;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> SecretValuesAsync(
        ISecretCatalog catalog,
        ISecretStore secrets,
        WorkloadSnapshot app,
        CancellationToken cancellationToken)
    {
        var values = new List<string>();
        IReadOnlyList<SecretSnapshot> listed;
        try
        {
            listed = await catalog.ListAsync(app.TeamId, app.Environment, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Secret names were not loaded for log redaction. {ExceptionType}", exception.GetType().Name);
            return values;
        }

        foreach (var secret in listed)
        {
            try
            {
                var value = await secrets.ReadAsync(new SecretAddress(app.Environment, secret.Path), cancellationToken);
                if (!string.IsNullOrEmpty(value))
                {
                    values.Add(value);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning("A secret value was not loaded for log redaction. {ExceptionType}", exception.GetType().Name);
            }
        }

        return values;
    }

    private TimeSpan PollInterval()
    {
        if (int.TryParse(_configuration["Logs:PollSeconds"], out var seconds) && seconds is >= 5 and <= 3600)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.FromSeconds(20);
    }
}
