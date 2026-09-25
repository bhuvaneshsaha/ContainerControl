using System.Collections.Concurrent;
using ContainerControl.Modules.Applications.Workloads;
using ContainerControl.Modules.Platform.Alerts;
using ContainerControl.Modules.Platform.Engine;
using ContainerControl.Modules.Platform.Hosts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContainerControl.Modules.Runtime.Inspection;

public static class HealthAlertGate
{
    public static bool ShouldAlert(string? previous, string? current) =>
        string.Equals(current, "unhealthy", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(previous, "unhealthy", StringComparison.OrdinalIgnoreCase);
}

public sealed class ServiceHealthMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiceHealthMonitor> _logger;
    private readonly ConcurrentDictionary<string, string> _status = new(StringComparer.Ordinal);

    public ServiceHealthMonitor(
        IServiceScopeFactory scopes,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<ServiceHealthMonitor> logger)
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
                await CheckOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning("Health alerts did not finish. {ExceptionType}", exception.GetType().Name);
            }

            await Task.Delay(poll, stoppingToken);
        }
    }

    public async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var apps = await services.GetRequiredService<IWorkloadStore>().ListAllAsync(cancellationToken);
        var hosts = services.GetRequiredService<IDockerHostLookup>();
        var engine = services.GetRequiredService<IDockerEngine>();
        var alerts = services.GetRequiredService<IAlertPublisher>();
        foreach (var app in apps)
        {
            if (!string.Equals(app.Status, "running", StringComparison.Ordinal))
            {
                continue;
            }

            var host = await hosts.FindAsync(app.HostId, cancellationToken);
            if (host is null)
            {
                continue;
            }

            IReadOnlyList<EngineContainer> containers;
            try
            {
                containers = await engine.ListByLabelAsync(new DockerEndpoint(host.Endpoint), ManagedLabels.Application + "=" + app.Id, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning("Health check skipped {ApplicationId}. {ExceptionType}", app.Id, exception.GetType().Name);
                continue;
            }

            foreach (var container in containers.Where(item => item.Running))
            {
                if (string.Equals(container.Label(ManagedLabels.Role), ManagedLabels.SlotRouterRole, StringComparison.Ordinal))
                {
                    continue;
                }

                string? health;
                try
                {
                    health = await engine.ReadHealthStatusAsync(new DockerEndpoint(host.Endpoint), container.Id, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogWarning("Health status was not read for {ContainerId}. {ExceptionType}", container.Id, exception.GetType().Name);
                    continue;
                }

                var previous = _status.TryGetValue(container.Id, out var known) ? known : null;
                if (!string.IsNullOrEmpty(health))
                {
                    _status[container.Id] = health;
                }

                if (!HealthAlertGate.ShouldAlert(previous, health))
                {
                    continue;
                }

                var service = container.Label(ManagedLabels.Service) ?? container.Name.TrimStart('/');
                try
                {
                    await alerts.PublishAsync(
                        new AlertNotice(AlertKinds.ServiceUnhealthy, app.Id, app.Name, "Service " + service + " is unhealthy."),
                        cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogWarning("Unhealthy alert was not sent for {ApplicationId}. {ExceptionType}", app.Id, exception.GetType().Name);
                }
            }
        }
    }

    private TimeSpan PollInterval()
    {
        if (int.TryParse(_configuration["Alerts:PollSeconds"], out var seconds) && seconds is >= 10 and <= 3600)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.FromSeconds(30);
    }
}
